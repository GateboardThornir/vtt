namespace Vtt.Server.Table;

/// <summary>Whether a line was spoken by the character or by the person playing them.</summary>
/// <remarks>
/// A property of the message rather than a separate channel: the spec asks for the distinction, not
/// for two rooms, and one ordered sequence is what makes the history readable afterwards.
/// </remarks>
public enum ChatVoice
{
    InCharacter,
    OutOfCharacter,
}

/// <summary>
/// Something somebody said at a table.
/// </summary>
/// <remarks>
/// Persisted before it is broadcast. A message shown but not stored vanishes on refresh, and this
/// is a record of play that people read back.
/// </remarks>
public class ChatMessage
{
    public const int BodyMaxLength = 2000;

    private ChatMessage()
    {
    }

    public Guid Id { get; private set; }

    public Guid SessionId { get; private set; }

    public Guid AuthorUserId { get; private set; }

    public string Body { get; private set; } = null!;

    public ChatVoice Voice { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static ChatMessage Say(
        Guid sessionId,
        Guid authorUserId,
        string body,
        ChatVoice voice,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            SessionId = sessionId,
            AuthorUserId = authorUserId,
            Body = body,
            Voice = voice,
            CreatedAt = now,
        };
}
