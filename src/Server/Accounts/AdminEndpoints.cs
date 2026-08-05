using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Vtt.Server.Accounts;

public sealed record SetAccountStateRequest(AccountState State);

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAccountAdministration(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/accounts");

        group.MapGet("/", ListAllAsync);
        group.MapGet("/pending", ListPendingAsync);
        group.MapPut("/{id:guid}/state", SetStateAsync);
        group.MapPost("/{id:guid}/recovery-code", IssueRecoveryCodeAsync);

        return endpoints;
    }

    private static async Task<Results<Ok<IReadOnlyList<AccountSummary>>, UnauthorizedHttpResult, ForbidHttpResult>>
        ListAllAsync(ClaimsPrincipal principal, IAccountAdministration accounts, CancellationToken cancellationToken) =>
        await CheckAsync(principal, accounts, cancellationToken) switch
        {
            Access.Unauthenticated => TypedResults.Unauthorized(),
            Access.Forbidden => TypedResults.Forbid(),
            _ => TypedResults.Ok(await accounts.AllAsync(cancellationToken)),
        };

    private static async Task<Results<Ok<IReadOnlyList<AccountSummary>>, UnauthorizedHttpResult, ForbidHttpResult>>
        ListPendingAsync(ClaimsPrincipal principal, IAccountAdministration accounts, CancellationToken cancellationToken) =>
        // The listings are the real disclosure risk in this group, not the mutations: approving
        // somebody you should not have is loud, and quietly reading the roster of an
        // invitation-only platform is not.
        await CheckAsync(principal, accounts, cancellationToken) switch
        {
            Access.Unauthenticated => TypedResults.Unauthorized(),
            Access.Forbidden => TypedResults.Forbid(),
            _ => TypedResults.Ok(await accounts.PendingAsync(cancellationToken)),
        };

    private static async Task<Results<NoContent, NotFound, Conflict<string>, UnauthorizedHttpResult, ForbidHttpResult>>
        SetStateAsync(
            Guid id,
            SetAccountStateRequest request,
            ClaimsPrincipal principal,
            IAccountAdministration accounts,
            CancellationToken cancellationToken)
    {
        switch (await CheckAsync(principal, accounts, cancellationToken))
        {
            case Access.Unauthenticated:
                return TypedResults.Unauthorized();

            case Access.Forbidden:
                return TypedResults.Forbid();

            default:
                return await accounts.SetStateAsync(id, request.State, cancellationToken) switch
                {
                    TransitionResult.Applied => TypedResults.NoContent(),
                    TransitionResult.NoSuchAccount => TypedResults.NotFound(),
                    _ => TypedResults.Conflict("That account cannot move to that state."),
                };
        }
    }

    private static async Task<Results<Ok<IssuedRecoveryCode>, NotFound, UnauthorizedHttpResult, ForbidHttpResult>>
        IssueRecoveryCodeAsync(
            Guid id,
            ClaimsPrincipal principal,
            IAccountAdministration accounts,
            IRecoveryService recovery,
            CancellationToken cancellationToken)
    {
        switch (await CheckAsync(principal, accounts, cancellationToken))
        {
            case Access.Unauthenticated:
                return TypedResults.Unauthorized();

            case Access.Forbidden:
                return TypedResults.Forbid();

            default:
                var issued = await recovery.IssueAsync(
                    id,
                    SessionCookie.UserIdOf(principal)!.Value,
                    cancellationToken);

                // Shown once. The administrator passes it on out of band and never learns the
                // password chosen with it.
                return issued is null ? TypedResults.NotFound() : TypedResults.Ok(issued);
        }
    }

    private enum Access
    {
        Granted,
        Unauthenticated,
        Forbidden,
    }

    /// <remarks>
    /// The role is read from the database on every call rather than taken from the cookie. 013 put
    /// no roles in the cookie deliberately: a cookie is frozen until it is reissued, so an
    /// administrator demoted today would keep their powers until they next signed in.
    /// </remarks>
    private static async Task<Access> CheckAsync(
        ClaimsPrincipal principal,
        IAccountAdministration accounts,
        CancellationToken cancellationToken)
    {
        var id = SessionCookie.UserIdOf(principal);

        if (id is null)
        {
            return Access.Unauthenticated;
        }

        return await accounts.IsAdministratorAsync(id.Value, cancellationToken)
            ? Access.Granted
            : Access.Forbidden;
    }
}
