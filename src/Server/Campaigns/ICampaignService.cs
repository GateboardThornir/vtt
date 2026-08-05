namespace Vtt.Server.Campaigns;

public interface ICampaignService
{
    Task<CampaignSummary> CreateAsync(
        string name,
        Guid masterUserId,
        string systemId,
        string systemVersion,
        CancellationToken cancellationToken = default);

    /// <summary>Campaigns this account can see. Nothing else exists as far as it is concerned.</summary>
    Task<IReadOnlyList<CampaignSummary>> VisibleToAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One campaign, or null if this account may not see it.
    /// </summary>
    /// <remarks>
    /// Null covers both "no such campaign" and "not yours", deliberately: the caller turns both
    /// into a 404. A 403 would confirm that a campaign with that id exists, and the list of a
    /// private group's campaigns is not public information.
    /// </remarks>
    Task<CampaignSummary?> VisibleToAsync(
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
