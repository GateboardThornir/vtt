namespace Vtt.Server.Sessions;

/// <summary>
/// One scheduled live event within a campaign.
/// </summary>
/// <remarks>
/// Named <c>PlaySession</c> rather than <c>Session</c> to keep it distinct from the sign-in session
/// in <c>Accounts</c>. They are unrelated concepts and sharing a name in one codebase invites the
/// wrong one being reached for.
/// <para>
/// Everything the live table does from Phase 2 hangs off one of these being open, so closing a
/// session must never delete anything — the event log and snapshots will reference these rows.
/// </para>
/// </remarks>
public class PlaySession
{
    public const int TitleMaxLength = 120;

    private PlaySession()
    {
    }

    public Guid Id { get; private set; }

    public Guid CampaignId { get; private set; }

    public string Title { get; private set; } = null!;

    public SessionState State { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? OpenedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public static PlaySession Plan(Guid campaignId, string title, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            CampaignId = campaignId,
            Title = title,
            State = SessionState.Planned,
            CreatedAt = now,
        };

    /// <remarks>
    /// Forward only, and the same small transition table used for account state and membership.
    /// Reopening a closed session is deliberately not allowed: whether it should be is a real
    /// question, and nothing needs an answer yet.
    /// </remarks>
    public bool TransitionTo(SessionState state, DateTimeOffset now)
    {
        var allowed = (State, state) switch
        {
            (SessionState.Planned, SessionState.Open) => true,
            (SessionState.Planned, SessionState.Closed) => true,
            (SessionState.Open, SessionState.Closed) => true,
            _ => false,
        };

        if (!allowed)
        {
            return false;
        }

        State = state;

        if (state == SessionState.Open)
        {
            OpenedAt = now;
        }
        else
        {
            ClosedAt = now;
        }

        return true;
    }
}
