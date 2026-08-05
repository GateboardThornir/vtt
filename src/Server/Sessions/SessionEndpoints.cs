using System.Security.Claims;
using Vtt.Server.Accounts;

namespace Vtt.Server.Sessions;

public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapPlaySessions(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/campaigns/{campaignId:guid}/sessions")
            .RequireAuthorization(AccountPolicies.ActiveAccount);

        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{sessionId:guid}/state", SetStateAsync);

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid campaignId,
        ClaimsPrincipal principal,
        ISessionService sessions,
        CancellationToken cancellationToken)
    {
        var found = await sessions.ForCampaignAsync(campaignId, Caller(principal), cancellationToken);

        return found is null ? Results.NotFound() : Results.Ok(found);
    }

    private static async Task<IResult> CreateAsync(
        Guid campaignId,
        CreateSessionRequest request,
        ClaimsPrincipal principal,
        ISessionService sessions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > PlaySession.TitleMaxLength)
        {
            return Results.BadRequest("title_invalid");
        }

        var (outcome, session) = await sessions.CreateAsync(
            campaignId,
            Caller(principal),
            request.Title,
            cancellationToken);

        return outcome == SessionOutcome.Done
            ? Results.Created($"/api/campaigns/{campaignId}/sessions/{session!.Id}", session)
            : Translate(outcome);
    }

    private static async Task<IResult> SetStateAsync(
        Guid campaignId,
        Guid sessionId,
        SetSessionStateRequest request,
        ClaimsPrincipal principal,
        ISessionService sessions,
        CancellationToken cancellationToken) =>
        Translate(await sessions.SetStateAsync(
            campaignId,
            sessionId,
            Caller(principal),
            request.State,
            cancellationToken));

    private static Guid Caller(ClaimsPrincipal principal) => SessionCookie.UserIdOf(principal)!.Value;

    /// <remarks>
    /// Not on the roster is a 404, matching the campaign itself: the caller is not entitled to know
    /// the campaign exists. On the roster but not the Master is a 403 — they already know.
    /// </remarks>
    private static IResult Translate(SessionOutcome outcome) => outcome switch
    {
        SessionOutcome.Done => Results.NoContent(),
        SessionOutcome.NotVisible => Results.NotFound(),
        SessionOutcome.NotTheMaster => Results.Forbid(),
        _ => Results.Conflict("not_allowed"),
    };
}

public static class PlaySessionServices
{
    public static IServiceCollection AddPlaySessions(this IServiceCollection services) =>
        services.AddScoped<ISessionService, SessionService>();
}
