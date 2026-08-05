using Microsoft.EntityFrameworkCore;
using Vtt.Server.Infrastructure;

namespace Vtt.Server.Campaigns;

internal sealed class CampaignService(VttDbContext context, TimeProvider clock) : ICampaignService
{
    public async Task<CampaignSummary> CreateAsync(
        string name,
        Guid masterUserId,
        string systemId,
        string systemVersion,
        CancellationToken cancellationToken = default)
    {
        var campaign = Campaign.Create(name.Trim(), masterUserId, systemId, systemVersion, clock.GetUtcNow());

        context.Set<Campaign>().Add(campaign);
        await context.SaveChangesAsync(cancellationToken);

        return Summarise(campaign);
    }

    public async Task<IReadOnlyList<CampaignSummary>> VisibleToAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await Visible(userId)
            .OrderBy(campaign => campaign.CreatedAt)
            .Select(campaign => new CampaignSummary(
                campaign.Id,
                campaign.Name,
                campaign.SystemId,
                campaign.SystemVersion,
                campaign.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<CampaignSummary?> VisibleToAsync(
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await Visible(userId)
            .Where(campaign => campaign.Id == campaignId)
            .Select(campaign => new CampaignSummary(
                campaign.Id,
                campaign.Name,
                campaign.SystemId,
                campaign.SystemVersion,
                campaign.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);

    /// <remarks>
    /// The single place that decides what a given account can see. Every read goes through it, so
    /// task 021 widens visibility to the roster by changing this one predicate rather than by
    /// remembering to update each query.
    /// </remarks>
    private IQueryable<Campaign> Visible(Guid userId) =>
        context.Set<Campaign>()
            .AsNoTracking()
            .Where(campaign => campaign.MasterUserId == userId);

    private static CampaignSummary Summarise(Campaign campaign) =>
        new(campaign.Id, campaign.Name, campaign.SystemId, campaign.SystemVersion, campaign.CreatedAt);
}
