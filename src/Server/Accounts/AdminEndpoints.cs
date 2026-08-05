using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Vtt.Server.Accounts;

public sealed record SetAccountStateRequest(AccountState State);

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAccountAdministration(this IEndpointRouteBuilder endpoints)
    {
        // Declared once for the group. Task 014 checked the role inside each handler; the trouble
        // with that is that an endpoint which forgot to call the guard looks exactly like one that
        // remembered, whereas an endpoint missing from a protected group is visible.
        var group = endpoints
            .MapGroup("/api/admin/accounts")
            .RequireAuthorization(AccountPolicies.Administrator);

        group.MapGet("/", ListAllAsync);
        group.MapGet("/pending", ListPendingAsync);
        group.MapPut("/{id:guid}/state", SetStateAsync);
        group.MapPost("/{id:guid}/recovery-code", IssueRecoveryCodeAsync);

        endpoints
            .MapPost("/api/admin/invites", IssueInviteAsync)
            .RequireAuthorization(AccountPolicies.Administrator);

        return endpoints;
    }

    /// <remarks>
    /// The missing half of task 011: the service could mint invites from the first day and nothing
    /// could reach it, so closing the loop needed SQL by hand. The plaintext is returned once and
    /// is unrecoverable afterwards, which the screen has to make obvious.
    /// </remarks>
    private static async Task<Ok<IssuedInvite>> IssueInviteAsync(
        ClaimsPrincipal principal,
        IInviteService invites,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await invites.IssueAsync(SessionCookie.UserIdOf(principal)!.Value, cancellationToken));

    private static async Task<Ok<IReadOnlyList<AccountSummary>>> ListAllAsync(
        IAccountAdministration accounts,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await accounts.AllAsync(cancellationToken));

    /// <remarks>
    /// The listings are the real disclosure risk in this group, not the mutations: approving
    /// somebody you should not have is loud, and quietly reading the roster of an invitation-only
    /// platform is not.
    /// </remarks>
    private static async Task<Ok<IReadOnlyList<AccountSummary>>> ListPendingAsync(
        IAccountAdministration accounts,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await accounts.PendingAsync(cancellationToken));

    private static async Task<Results<NoContent, NotFound, Conflict<string>>> SetStateAsync(
        Guid id,
        SetAccountStateRequest request,
        IAccountAdministration accounts,
        CancellationToken cancellationToken) =>
        await accounts.SetStateAsync(id, request.State, cancellationToken) switch
        {
            TransitionResult.Applied => TypedResults.NoContent(),
            TransitionResult.NoSuchAccount => TypedResults.NotFound(),
            _ => TypedResults.Conflict("That account cannot move to that state."),
        };

    private static async Task<Results<Ok<IssuedRecoveryCode>, NotFound>> IssueRecoveryCodeAsync(
        Guid id,
        ClaimsPrincipal principal,
        IRecoveryService recovery,
        CancellationToken cancellationToken)
    {
        // Shown once. The administrator passes it on out of band and never learns the password
        // chosen with it.
        var issued = await recovery.IssueAsync(id, SessionCookie.UserIdOf(principal)!.Value, cancellationToken);

        return issued is null ? TypedResults.NotFound() : TypedResults.Ok(issued);
    }
}
