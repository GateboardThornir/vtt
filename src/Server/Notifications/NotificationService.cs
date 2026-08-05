using Microsoft.EntityFrameworkCore;
using Vtt.Server.Infrastructure;

namespace Vtt.Server.Notifications;

internal sealed class NotificationService(VttDbContext context, TimeProvider clock) : INotificationService
{
    public async Task RaiseAsync(
        Guid userId,
        NotificationKind kind,
        string? subject,
        CancellationToken cancellationToken = default)
    {
        context.Set<Notification>().Add(Notification.For(userId, kind, subject, clock.GetUtcNow()));

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationView>> ForAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await context.Set<Notification>()
            .AsNoTracking()
            .Where(notification => notification.UserId == userId)
            // Ordered on the entity, projected last — see .claude/rules/backend.md.
            .OrderByDescending(notification => notification.CreatedAt)
            .Select(notification => new NotificationView(
                notification.Id,
                notification.Kind,
                notification.Subject,
                notification.CreatedAt,
                notification.ReadAt != null))
            .ToListAsync(cancellationToken);

    public Task<int> UnreadCountAsync(Guid userId, CancellationToken cancellationToken = default) =>
        context.Set<Notification>()
            .CountAsync(
                notification => notification.UserId == userId && notification.ReadAt == null,
                cancellationToken);

    public async Task<bool> MarkReadAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        // The recipient is part of the WHERE clause rather than a check beforehand: knowing a
        // notification's identifier must not be enough to write to somebody else's row.
        var affected = await context.Set<Notification>()
            .Where(notification =>
                notification.Id == notificationId &&
                notification.UserId == userId &&
                notification.ReadAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(notification => notification.ReadAt, clock.GetUtcNow()),
                cancellationToken);

        return affected > 0;
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await context.Set<Notification>()
            .Where(notification => notification.UserId == userId && notification.ReadAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(notification => notification.ReadAt, clock.GetUtcNow()),
                cancellationToken);
}
