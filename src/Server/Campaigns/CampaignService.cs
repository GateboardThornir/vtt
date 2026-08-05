using System.Linq.Expressions;
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
        var now = clock.GetUtcNow();
        var campaign = Campaign.Create(name.Trim(), systemId, systemVersion, now);

        // The campaign and its Master's roster row are created together. A campaign with no Master
        // is not a state the system should be able to observe, even briefly.
        context.Set<Campaign>().Add(campaign);
        context.Set<CampaignMember>().Add(CampaignMember.ForMaster(campaign.Id, masterUserId, now));

        await context.SaveChangesAsync(cancellationToken);

        return new CampaignSummary(
            campaign.Id,
            campaign.Name,
            campaign.SystemId,
            campaign.SystemVersion,
            campaign.CreatedAt,
            CampaignRole.Master);
    }

    public async Task<IReadOnlyList<CampaignSummary>> VisibleToAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await Visible(userId, campaign => true).ToListAsync(cancellationToken);

    public async Task<CampaignSummary?> VisibleToAsync(
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await Visible(userId, campaign => campaign.Id == campaignId)
            .SingleOrDefaultAsync(cancellationToken);

    /// <remarks>
    /// The single place that decides what an account can see, widened from "you master it" to "you
    /// are actually on the roster". <see cref="MembershipState.Active"/> and nothing else: an
    /// invitation that has not been accepted confers no access, and treating any row in the
    /// membership table as membership is the easiest mistake available here.
    /// </remarks>
    private IQueryable<CampaignSummary> Visible(
        Guid userId,
        Expression<Func<Campaign, bool>> filter) =>
        from campaign in context.Set<Campaign>().AsNoTracking().Where(filter)
        join member in context.Set<CampaignMember>().AsNoTracking()
            on campaign.Id equals member.CampaignId
        where member.UserId == userId && member.State == MembershipState.Active
        // Ordered here, before the projection. Ordering an already-projected record does not
        // translate to SQL and fails at runtime — see .claude/rules/backend.md.
        orderby campaign.CreatedAt
        select new CampaignSummary(
            campaign.Id,
            campaign.Name,
            campaign.SystemId,
            campaign.SystemVersion,
            campaign.CreatedAt,
            member.Role);
}
