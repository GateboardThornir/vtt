namespace Vtt.Server.Table;

/// <summary>A message as a client sees it.</summary>
public sealed record ChatLine(
    Guid Id,
    Guid AuthorUserId,
    string AuthorUsername,
    string Body,
    ChatVoice Voice,
    DateTimeOffset CreatedAt);

public interface IChatService
{
    /// <summary>Stores a message, or null if the caller may not speak at that table.</summary>
    Task<ChatLine?> SayAsync(
        Guid sessionId,
        Guid authorUserId,
        string body,
        ChatVoice voice,
        CancellationToken cancellationToken = default);

    /// <summary>Recent history, or null if the caller may not read it.</summary>
    Task<IReadOnlyList<ChatLine>?> HistoryAsync(
        Guid sessionId,
        Guid callerId,
        int limit = 200,
        CancellationToken cancellationToken = default);
}
