using Microsoft.EntityFrameworkCore;
using Vtt.Server.Accounts;
using Vtt.Server.Infrastructure;
using Vtt.Server.Notifications;

namespace Vtt.Server.Campaigns;

internal sealed class RosterService(
    VttDbContext context,
    ICampaignRoles roles,
    INotificationService notifications,
    TimeProvider clock) : IRosterService
{
    public async Task<IReadOnlyList<RosterEntry>?> OfAsync(
        Guid campaignId,
        Guid callerId,
        CancellationToken cancellationToken = default)
    {
        if (await roles.RoleOfAsync(campaignId, callerId, cancellationToken) is null)
        {
            // Null, not an empty list: the caller is not entitled to know the campaign exists.
            return null;
        }

        // Ordered on the entity, projected last. The other way round does not translate: EF cannot
        // turn OrderBy(x => new RosterEntry(...).Username) into SQL, because the object being
        // ordered by does not exist in the database. Same trap as AccountAdministration.
        return await (from member in context.Set<CampaignMember>().AsNoTracking()
                      join user in context.Set<User>().AsNoTracking() on member.UserId equals user.Id
                      where member.CampaignId == campaignId
                      orderby user.Username
                      select new RosterEntry(user.Id, user.Username, member.Role, member.State))
            .ToListAsync(cancellationToken);
    }

    public async Task<RosterOutcome> InviteAsync(
        Guid campaignId,
        Guid callerId,
        string username,
        CancellationToken cancellationToken = default)
    {
        var guard = await RequireMasterAsync(campaignId, callerId, cancellationToken);

        if (guard != RosterOutcome.Done)
        {
            return guard;
        }

        var normalised = User.Normalize(username);
        var invitee = await context.Set<User>()
            .Where(user => user.UsernameNormalized == normalised && user.State == AccountState.Active)
            .Select(user => user.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (invitee == Guid.Empty)
        {
            return RosterOutcome.NoSuchAccount;
        }

        var existing = await context.Set<CampaignMember>()
            .SingleOrDefaultAsync(
                member => member.CampaignId == campaignId && member.UserId == invitee,
                cancellationToken);

        if (existing is null)
        {
            context.Set<CampaignMember>().Add(CampaignMember.Invite(campaignId, invitee, clock.GetUtcNow()));
        }
        else if (!existing.TransitionTo(MembershipState.Invited, clock.GetUtcNow()))
        {
            // Already invited, or already a member. Re-inviting reuses the row rather than
            // creating a second, which the unique index would refuse anyway.
            return RosterOutcome.NotAllowed;
        }

        await context.SaveChangesAsync(cancellationToken);

        // Raised where the invitation happens, rather than through an event bus: three publishers
        // do not justify the indirection, and the flow that knows the campaign's name is here.
        var name = await context.Set<Campaign>()
            .Where(campaign => campaign.Id == campaignId)
            .Select(campaign => campaign.Name)
            .SingleAsync(cancellationToken);

        await notifications.RaiseAsync(invitee, NotificationKind.CampaignInvitation, name, cancellationToken);

        return RosterOutcome.Done;
    }

    public Task<RosterOutcome> RespondAsync(
        Guid campaignId,
        Guid callerId,
        bool accept,
        CancellationToken cancellationToken = default) =>
        MoveAsync(
            campaignId,
            callerId,
            accept ? MembershipState.Active : MembershipState.Declined,
            cancellationToken);

    public async Task<RosterOutcome> LeaveAsync(
        Guid campaignId,
        Guid callerId,
        CancellationToken cancellationToken = default)
    {
        // The Master is the one member who cannot leave: a campaign with no Master has nobody who
        // can run it and nobody who can hand it over.
        if (await roles.IsMasterAsync(campaignId, callerId, cancellationToken))
        {
            return RosterOutcome.NotAllowed;
        }

        return await MoveAsync(campaignId, callerId, MembershipState.Left, cancellationToken);
    }

    public async Task<RosterOutcome> RemoveAsync(
        Guid campaignId,
        Guid callerId,
        Guid memberUserId,
        CancellationToken cancellationToken = default)
    {
        var guard = await RequireMasterAsync(campaignId, callerId, cancellationToken);

        if (guard != RosterOutcome.Done)
        {
            return guard;
        }

        if (callerId == memberUserId)
        {
            return RosterOutcome.NotAllowed;
        }

        return await MoveAsync(campaignId, memberUserId, MembershipState.Left, cancellationToken);
    }

    public async Task<IReadOnlyList<CampaignSummary>> PendingInvitationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await (from campaign in context.Set<Campaign>().AsNoTracking()
               join member in context.Set<CampaignMember>().AsNoTracking()
                   on campaign.Id equals member.CampaignId
               where member.UserId == userId && member.State == MembershipState.Invited
               orderby campaign.CreatedAt
               select new CampaignSummary(
                   campaign.Id,
                   campaign.Name,
                   campaign.SystemId,
                   campaign.SystemVersion,
                   campaign.CreatedAt,
                   member.Role))
            .ToListAsync(cancellationToken);

    private async Task<RosterOutcome> RequireMasterAsync(
        Guid campaignId,
        Guid callerId,
        CancellationToken cancellationToken)
    {
        var role = await roles.RoleOfAsync(campaignId, callerId, cancellationToken);

        return role switch
        {
            // Not on the roster at all: the campaign does not exist as far as this caller knows.
            null => RosterOutcome.NoSuchCampaign,
            CampaignRole.Master => RosterOutcome.Done,
            _ => RosterOutcome.NotTheMaster,
        };
    }

    private async Task<RosterOutcome> MoveAsync(
        Guid campaignId,
        Guid userId,
        MembershipState state,
        CancellationToken cancellationToken)
    {
        var member = await context.Set<CampaignMember>()
            .SingleOrDefaultAsync(
                candidate => candidate.CampaignId == campaignId && candidate.UserId == userId,
                cancellationToken);

        if (member is null)
        {
            return RosterOutcome.NoSuchCampaign;
        }

        if (!member.TransitionTo(state, clock.GetUtcNow()))
        {
            return RosterOutcome.NotAllowed;
        }

        await context.SaveChangesAsync(cancellationToken);

        return RosterOutcome.Done;
    }
}
