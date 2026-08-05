namespace Vtt.Server.Campaigns;

/// <summary>What a caller sends to create a campaign.</summary>
public sealed record CreateCampaignRequest(string? Name, string? SystemId, string? SystemVersion);

/// <summary>A campaign as one of its members sees it, including what they are to it.</summary>
public sealed record CampaignSummary(
    Guid Id,
    string Name,
    string SystemId,
    string SystemVersion,
    DateTimeOffset CreatedAt,
    CampaignRole Role);

/// <summary>Somebody on a campaign's roster.</summary>
public sealed record RosterEntry(Guid UserId, string Username, CampaignRole Role, MembershipState State);

public sealed record InviteMemberRequest(string? Username);

public sealed record RespondToInvitationRequest(bool Accept);

public static class CampaignRules
{
    public const int NameMinLength = 1;

    public static bool IsWellFormedName(string? name) =>
        !string.IsNullOrWhiteSpace(name) &&
        name.Trim().Length >= NameMinLength &&
        name.Trim().Length <= Campaign.NameMaxLength;

    public static bool IsWellFormedSystem(string? systemId, string? version) =>
        !string.IsNullOrWhiteSpace(systemId) &&
        systemId.Length <= Campaign.SystemIdMaxLength &&
        !string.IsNullOrWhiteSpace(version) &&
        version.Length <= Campaign.SystemVersionMaxLength;
}
