namespace Vtt.Server.Campaigns;

public interface IRosterService
{
    /// <summary>The roster, visible to anyone actively on it.</summary>
    Task<IReadOnlyList<RosterEntry>?> OfAsync(
        Guid campaignId,
        Guid callerId,
        CancellationToken cancellationToken = default);

    /// <summary>Master only. Inviting somebody already invited reuses their row.</summary>
    Task<RosterOutcome> InviteAsync(
        Guid campaignId,
        Guid callerId,
        string username,
        CancellationToken cancellationToken = default);

    /// <summary>The invitee accepts or declines their own invitation.</summary>
    Task<RosterOutcome> RespondAsync(
        Guid campaignId,
        Guid callerId,
        bool accept,
        CancellationToken cancellationToken = default);

    /// <summary>Leaving voluntarily. The Master cannot: the campaign would have none.</summary>
    Task<RosterOutcome> LeaveAsync(
        Guid campaignId,
        Guid callerId,
        CancellationToken cancellationToken = default);

    /// <summary>Master only. Removing a member is the same transition as their leaving.</summary>
    Task<RosterOutcome> RemoveAsync(
        Guid campaignId,
        Guid callerId,
        Guid memberUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Campaigns this account has been asked to join and has not answered.</summary>
    Task<IReadOnlyList<CampaignSummary>> PendingInvitationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
