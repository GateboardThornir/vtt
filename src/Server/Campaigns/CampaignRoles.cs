using Microsoft.EntityFrameworkCore;
using Vtt.Server.Infrastructure;

namespace Vtt.Server.Campaigns;

internal sealed class CampaignRoles(VttDbContext context) : ICampaignRoles
{
    public async Task<CampaignRole?> RoleOfAsync(
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var roles = await context.Set<CampaignMember>()
            .AsNoTracking()
            .Where(member =>
                member.CampaignId == campaignId &&
                member.UserId == userId &&
                // Active membership only. An invitation that has not been accepted is not
                // membership, and neither is one that was declined or left.
                member.State == MembershipState.Active)
            .Select(member => member.Role)
            .ToListAsync(cancellationToken);

        return roles.Count == 0 ? null : roles[0];
    }

    public async Task<bool> IsMasterAsync(
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await RoleOfAsync(campaignId, userId, cancellationToken) == CampaignRole.Master;
}
