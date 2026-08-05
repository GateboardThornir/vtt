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

        return endpoints;
    }

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
        services.AddScoped<ICampaignService, CampaignService>();
}
