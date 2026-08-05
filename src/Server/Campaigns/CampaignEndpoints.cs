using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Vtt.Server.Accounts;

namespace Vtt.Server.Campaigns;

public static class CampaignEndpoints
{
    public static IEndpointRouteBuilder MapCampaigns(this IEndpointRouteBuilder endpoints)
    {
        // Any active member may create a campaign — doing so is what makes them a Master. There is
        // no platform-level permission for it, by design.
        var group = endpoints
            .MapGroup("/api/campaigns")
            .RequireAuthorization(AccountPolicies.ActiveAccount);

        group.MapPost("/", CreateAsync);
        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);

        group.MapGet("/invitations", PendingInvitationsAsync);
        group.MapGet("/{id:guid}/roster", RosterAsync);
        group.MapPost("/{id:guid}/roster", InviteAsync);
        group.MapPost("/{id:guid}/roster/response", RespondAsync);
        group.MapDelete("/{id:guid}/roster/me", LeaveAsync);
        group.MapDelete("/{id:guid}/roster/{userId:guid}", RemoveAsync);

        return endpoints;
    }

    private static async Task<Ok<IReadOnlyList<CampaignSummary>>> PendingInvitationsAsync(
        ClaimsPrincipal principal,
        IRosterService roster,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await roster.PendingInvitationsAsync(Caller(principal), cancellationToken));

    private static async Task<Results<Ok<IReadOnlyList<RosterEntry>>, NotFound>> RosterAsync(
        Guid id,
        ClaimsPrincipal principal,
        IRosterService roster,
        CancellationToken cancellationToken)
    {
        var entries = await roster.OfAsync(id, Caller(principal), cancellationToken);

        return entries is null ? TypedResults.NotFound() : TypedResults.Ok(entries);
    }

    private static async Task<IResult> InviteAsync(
        Guid id,
        InviteMemberRequest request,
        ClaimsPrincipal principal,
        IRosterService roster,
        CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(request.Username)
            ? Results.BadRequest("username_required")
            : Translate(await roster.InviteAsync(id, Caller(principal), request.Username, cancellationToken));

    private static async Task<IResult> RespondAsync(
        Guid id,
        RespondToInvitationRequest request,
        ClaimsPrincipal principal,
        IRosterService roster,
        CancellationToken cancellationToken) =>
        Translate(await roster.RespondAsync(id, Caller(principal), request.Accept, cancellationToken));

    private static async Task<IResult> LeaveAsync(
        Guid id,
        ClaimsPrincipal principal,
        IRosterService roster,
        CancellationToken cancellationToken) =>
        Translate(await roster.LeaveAsync(id, Caller(principal), cancellationToken));

    private static async Task<IResult> RemoveAsync(
        Guid id,
        Guid userId,
        ClaimsPrincipal principal,
        IRosterService roster,
        CancellationToken cancellationToken) =>
        Translate(await roster.RemoveAsync(id, Caller(principal), userId, cancellationToken));

    private static Guid Caller(ClaimsPrincipal principal) => SessionCookie.UserIdOf(principal)!.Value;

    /// <remarks>
    /// A caller who is not on the roster gets 404, never 403: a 403 would confirm the campaign
    /// exists. Somebody who <em>is</em> on it but is not the Master gets 403, because they already
    /// know it exists and hiding the reason would only confuse them.
    /// </remarks>
    private static IResult Translate(RosterOutcome outcome) => outcome switch
    {
        RosterOutcome.Done => Results.NoContent(),
        RosterOutcome.NoSuchCampaign => Results.NotFound(),
        RosterOutcome.NotTheMaster => Results.Forbid(),
        RosterOutcome.NoSuchAccount => Results.BadRequest("no_such_account"),
        _ => Results.Conflict("not_allowed"),
    };

    private static async Task<Results<Created<CampaignSummary>, BadRequest<string>>> CreateAsync(
        CreateCampaignRequest request,
        ClaimsPrincipal principal,
        ICampaignService campaigns,
        CancellationToken cancellationToken)
    {
        if (!CampaignRules.IsWellFormedName(request.Name))
        {
            return TypedResults.BadRequest("name_invalid");
        }

        if (!CampaignRules.IsWellFormedSystem(request.SystemId, request.SystemVersion))
        {
            return TypedResults.BadRequest("system_invalid");
        }

        var created = await campaigns.CreateAsync(
            request.Name!,
            SessionCookie.UserIdOf(principal)!.Value,
            request.SystemId!,
            request.SystemVersion!,
            cancellationToken);

        return TypedResults.Created($"/api/campaigns/{created.Id}", created);
    }

    private static async Task<Ok<IReadOnlyList<CampaignSummary>>> ListAsync(
        ClaimsPrincipal principal,
        ICampaignService campaigns,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await campaigns.VisibleToAsync(SessionCookie.UserIdOf(principal)!.Value, cancellationToken));

    /// <remarks>
    /// A campaign the caller may not see is a 404, not a 403. A 403 would confirm it exists, and
    /// which campaigns a private group runs is not public information.
    /// </remarks>
    private static async Task<Results<Ok<CampaignSummary>, NotFound>> GetAsync(
        Guid id,
        ClaimsPrincipal principal,
        ICampaignService campaigns,
        CancellationToken cancellationToken)
    {
        var campaign = await campaigns.VisibleToAsync(
            id,
            SessionCookie.UserIdOf(principal)!.Value,
            cancellationToken);

        return campaign is null ? TypedResults.NotFound() : TypedResults.Ok(campaign);
    }
}

public static class CampaignServices
{
    public static IServiceCollection AddCampaigns(this IServiceCollection services) =>
        services
            .AddScoped<ICampaignService, CampaignService>()
            .AddScoped<ICampaignRoles, CampaignRoles>()
            .AddScoped<IRosterService, RosterService>();
}
