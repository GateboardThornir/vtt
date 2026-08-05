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

        return endpoints;
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
