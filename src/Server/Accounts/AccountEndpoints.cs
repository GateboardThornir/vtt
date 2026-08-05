using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Vtt.Server.Accounts;

public static class AccountEndpoints
{
    /// <summary>
    /// Maps the Accounts module's HTTP surface, so <c>Program.cs</c> gains a line per module rather
    /// than a line per route.
    /// </summary>
    public static IEndpointRouteBuilder MapAccounts(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/registration", RegisterAsync);

        endpoints.MapPost("/api/session", SignInAsync);
        // Cast because a handler taking only HttpContext otherwise binds as a RequestDelegate,
        // whose return value is discarded — the 204 would never be written.
        endpoints.MapDelete("/api/session", (Delegate)SignOutAsync);
        endpoints.MapGet("/api/session", GetSession);

        endpoints.MapPost("/api/password-reset", RedeemRecoveryCodeAsync);

        return endpoints;
    }

    /// <remarks>
    /// Unauthenticated: whoever holds the code is, by construction, the person the administrator
    /// gave it to. Every failure returns one answer, because distinguishing them would confirm to a
    /// stranger that a code once existed for some account.
    /// </remarks>
    private static async Task<Results<NoContent, BadRequest<RegistrationError>>> RedeemRecoveryCodeAsync(
        PasswordResetRequest request,
        IRecoveryService recovery,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Code) || string.IsNullOrEmpty(request.NewPassword))
        {
            return TypedResults.BadRequest(new RegistrationError("code_invalid"));
        }

        return await recovery.RedeemAsync(request.Code, request.NewPassword, cancellationToken) switch
        {
            RecoveryOutcome.PasswordChanged => TypedResults.NoContent(),
            RecoveryOutcome.PasswordUnacceptable => TypedResults.BadRequest(new RegistrationError("password_too_short")),
            _ => TypedResults.BadRequest(new RegistrationError("code_invalid")),
        };
    }

    private static async Task<Results<Ok<SessionResponse>, UnauthorizedHttpResult, ProblemHttpResult>>
        SignInAsync(
            SignInRequest request,
            ISignInService signIn,
            HttpContext http,
            CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
        {
            return TypedResults.Unauthorized();
        }

        var (outcome, user) = await signIn.AuthenticateAsync(
            request.Username,
            request.Password,
            cancellationToken);

        switch (outcome)
        {
            case SignInOutcome.Succeeded when user is not null:
                await http.SignInAsync(SessionCookie.Scheme, SessionCookie.PrincipalFor(user));

                return TypedResults.Ok(new SessionResponse(user.Id, user.Username));

            // Both of these are only reachable once the password has been verified, so naming the
            // reason tells the holder nothing they have not already proved they know.
            case SignInOutcome.AwaitingApproval:
                return TypedResults.Problem(
                    title: "awaiting_approval",
                    detail: "This account is waiting for an administrator to approve it.",
                    statusCode: StatusCodes.Status403Forbidden);

            case SignInOutcome.Disabled:
                return TypedResults.Problem(
                    title: "account_disabled",
                    detail: "This account has been disabled.",
                    statusCode: StatusCodes.Status403Forbidden);

            // One answer for "no such account" and "wrong password" alike.
            default:
                return TypedResults.Unauthorized();
        }
    }

    private static async Task<NoContent> SignOutAsync(HttpContext http)
    {
        await http.SignOutAsync(SessionCookie.Scheme);

        return TypedResults.NoContent();
    }

    private static Results<Ok<SessionResponse>, UnauthorizedHttpResult> GetSession(ClaimsPrincipal principal)
    {
        var id = SessionCookie.UserIdOf(principal);

        return id is null
            ? TypedResults.Unauthorized()
            : TypedResults.Ok(new SessionResponse(id.Value, principal.Identity?.Name ?? string.Empty));
    }

    /// <remarks>
    /// Registering does not sign anybody in. There is no cookie, no token and no account identifier
    /// in the response — authentication is task 013's, and an endpoint that issued a session "for
    /// convenience" would put it outside the task that reviews it.
    /// </remarks>
    private static async Task<Results<Created, BadRequest<RegistrationError>, Conflict<RegistrationError>>>
        RegisterAsync(
            RegistrationRequest request,
            IRegistrationService registrations,
            CancellationToken cancellationToken)
    {
        if (!RegistrationRules.IsWellFormedUsername(request.Username))
        {
            return TypedResults.BadRequest(new RegistrationError("username_invalid"));
        }

        if (!RegistrationRules.IsAcceptablePassword(request.Password))
        {
            // The message says a length is required, never what was supplied.
            return TypedResults.BadRequest(new RegistrationError("password_too_short"));
        }

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return TypedResults.BadRequest(new RegistrationError("invite_invalid"));
        }

        var outcome = await registrations.RegisterAsync(
            request.Token,
            request.Username!,
            request.Password!,
            cancellationToken);

        return outcome switch
        {
            RegistrationOutcome.Registered => TypedResults.Created(),

            // Expired and already-used are stated plainly: a token is 256 bits of randomness, so
            // whoever presents a real one is its intended holder and is entitled to know why it
            // will not work. Anything unrecognised gets the generic answer, because confirming
            // whether an arbitrary string is a real token is the part that would help an attacker.
            RegistrationOutcome.InviteExpired => TypedResults.BadRequest(new RegistrationError("invite_expired")),
            RegistrationOutcome.InviteAlreadyUsed => TypedResults.BadRequest(new RegistrationError("invite_already_used")),
            RegistrationOutcome.UsernameTaken => TypedResults.Conflict(new RegistrationError("username_taken")),
            _ => TypedResults.BadRequest(new RegistrationError("invite_invalid")),
        };
    }
}
