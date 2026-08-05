namespace Vtt.Server.Notifications;

public sealed record NotificationView(
    Guid Id,
    NotificationKind Kind,
    string? Subject,
    DateTimeOffset CreatedAt,
    bool Read);

public interface INotificationService
{
    Task RaiseAsync(
        Guid userId,
        NotificationKind kind,
        string? subject,
        CancellationToken cancellationToken = default);

    /// <summary>The caller's own notifications, newest first. Never anybody else's.</summary>
    Task<IReadOnlyList<NotificationView>> ForAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<int> UnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks one notification read.
    /// </summary>
    /// <remarks>
    /// Scoped by recipient in the same statement that does the update, so a caller cannot mark
    /// somebody else's notification read by knowing its identifier.
    /// </remarks>
    Task<bool> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default);

    Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default);
}
