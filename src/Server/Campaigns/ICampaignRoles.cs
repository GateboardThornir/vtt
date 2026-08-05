namespace Vtt.Server.Campaigns;

/// <summary>
/// Answers one question: what is this account to this campaign?
/// </summary>
/// <remarks>
/// Moved here from task 016, which could not build it — campaigns arrived at 020 and the roster
/// here, so a resolver written then would have had nothing to read.
/// <para>
/// Every campaign-scoped permission check goes through this, from the roster below to fog of war at
/// task 067. Scattering comparisons against a master column instead is how the visibility rules
/// eventually acquire a hole that no test covers. It answers one question and must not grow into a
/// permission system.
/// </para>
/// <para>
/// It reads the roster and only the roster. A platform administrator is nothing to a campaign they
/// are not on, per <c>.claude/rules/security.md</c>.
/// </para>
/// </remarks>
public interface ICampaignRoles
{
    /// <summary>The caller's active role in the campaign, or null if they are not on its roster.</summary>
    Task<CampaignRole?> RoleOfAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken = default);

    Task<bool> IsMasterAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken = default);
}
