namespace Vtt.Server.Sessions;

public sealed record PlaySessionView(
    Guid Id,
    string Title,
    SessionState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset? OpenedAt,
    DateTimeOffset? ClosedAt);

public sealed record CreateSessionRequest(string? Title);

public sealed record SetSessionStateRequest(SessionState State);

public enum SessionOutcome
{
    Done,

    /// <summary>No such campaign or session, or the caller is not on the roster.</summary>
    NotVisible,

    NotTheMaster,

    /// <summary>The move is not legal, or another session is already open.</summary>
    NotAllowed,
}

public interface ISessionService
{
    /// <summary>Null when the caller is not on the campaign's roster.</summary>
    Task<IReadOnlyList<PlaySessionView>?> ForCampaignAsync(
        Guid campaignId,
        Guid callerId,
        CancellationToken cancellationToken = default);

    Task<(SessionOutcome Outcome, PlaySessionView? Session)> CreateAsync(
        Guid campaignId,
        Guid callerId,
        string title,
        CancellationToken cancellationToken = default);

    Task<SessionOutcome> SetStateAsync(
        Guid campaignId,
        Guid sessionId,
        Guid callerId,
        SessionState state,
        CancellationToken cancellationToken = default);
}
