namespace Vtt.Server.Notifications;

/// <summary>
/// Something that happened to one account while it was not looking.
/// </summary>
/// <remarks>
/// The platform has no email address for anybody, by design, so this is the only channel it has.
/// Every flow that would otherwise have sent a message ends here instead.
/// </remarks>
public class Notification
{
    public const int SubjectMaxLength = 120;

    private Notification()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>The one account this belongs to. No query ever returns anyone else's.</summary>
    public Guid UserId { get; private set; }

    public NotificationKind Kind { get; private set; }

    /// <summary>
    /// The one variable part — a campaign's name, today.
    /// </summary>
    /// <remarks>
    /// Deliberately a single string rather than a JSON blob: every kind so far needs exactly one
    /// piece of context, and a document would invite putting content in here that the recipient may
    /// not be entitled to see.
    /// </remarks>
    public string? Subject { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    public static Notification For(
        Guid userId,
        NotificationKind kind,
        string? subject,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Kind = kind,
            Subject = subject,
            CreatedAt = now,
        };

    public void MarkRead(DateTimeOffset now) => ReadAt ??= now;
}
