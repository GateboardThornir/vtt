namespace Vtt.Server.Campaigns;

/// <summary>
/// One account's place on one campaign's roster.
/// </summary>
/// <remarks>
/// Including the Master's. Task 020 kept the Master as a column on the campaign because with one
/// member there was nothing to join to; now there is, and keeping both would leave two sources of
/// truth for the same fact — which is the ordinary way an authorisation bug is born.
/// </remarks>
public class CampaignMember
{
    private CampaignMember()
    {
    }

    public Guid Id { get; private set; }

    public Guid CampaignId { get; private set; }

    public Guid UserId { get; private set; }

    public CampaignRole Role { get; private set; }

    public MembershipState State { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? RespondedAt { get; private set; }

    /// <summary>The Master's own row, created with the campaign and already active.</summary>
    public static CampaignMember ForMaster(Guid campaignId, Guid userId, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            CampaignId = campaignId,
            UserId = userId,
            Role = CampaignRole.Master,
            State = MembershipState.Active,
            CreatedAt = now,
            RespondedAt = now,
        };

    /// <summary>An invitation, which confers nothing until it is accepted.</summary>
    public static CampaignMember Invite(Guid campaignId, Guid userId, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            CampaignId = campaignId,
            UserId = userId,
            Role = CampaignRole.Player,
            State = MembershipState.Invited,
            CreatedAt = now,
        };

    /// <summary>
    /// Moves the membership on, or refuses.
    /// </summary>
    /// <remarks>
    /// The legal moves are a small table rather than scattered conditions, for the same reason
    /// task 014 did it for account state: one place to read when asking what can follow what.
    /// </remarks>
    public bool TransitionTo(MembershipState state, DateTimeOffset now)
    {
        var allowed = (State, state) switch
        {
            (MembershipState.Invited, MembershipState.Active) => true,    // accept
            (MembershipState.Invited, MembershipState.Declined) => true,  // decline
            (MembershipState.Active, MembershipState.Left) => true,       // leave, or be removed
            (MembershipState.Declined, MembershipState.Invited) => true,  // ask again
            (MembershipState.Left, MembershipState.Invited) => true,      // ask again
            _ => false,
        };

        if (!allowed)
        {
            return false;
        }

        State = state;
        RespondedAt = now;

        return true;
    }
}
