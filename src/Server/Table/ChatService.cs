using Microsoft.EntityFrameworkCore;
using Vtt.Server.Accounts;
using Vtt.Server.Infrastructure;

namespace Vtt.Server.Table;

internal sealed class ChatService(
    VttDbContext context,
    ITableAccess access,
    TimeProvider clock) : IChatService
{
    public async Task<ChatLine?> SayAsync(
        Guid sessionId,
        Guid authorUserId,
        string body,
        ChatVoice voice,
        CancellationToken cancellationToken = default)
    {
        var trimmed = body.Trim();

        if (trimmed.Length == 0 || trimmed.Length > ChatMessage.BodyMaxLength)
        {
            return null;
        }

        // Re-checked on every send, not only on join. Being in a group is not proof of anything
        // later: somebody removed from the roster mid-session must stop being able to talk, and
        // their connection is still sitting in the group until they notice.
        var participant = await access.AdmitAsync(sessionId, authorUserId, cancellationToken);

        if (participant is null)
        {
            return null;
        }

        var message = ChatMessage.Say(sessionId, authorUserId, trimmed, voice, clock.GetUtcNow());

        context.Set<ChatMessage>().Add(message);
        await context.SaveChangesAsync(cancellationToken);

        return new ChatLine(
            message.Id,
            participant.UserId,
            participant.Username,
            message.Body,
            message.Voice,
            message.CreatedAt);
    }

    public async Task<IReadOnlyList<ChatLine>?> HistoryAsync(
        Guid sessionId,
        Guid callerId,
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        if (await access.AdmitAsync(sessionId, callerId, cancellationToken) is null)
        {
            // Null rather than an empty list: history is a disclosure surface, and "nothing to see"
            // and "not yours to see" must not look the same to the caller.
            return null;
        }

        var lines = await (from message in context.Set<ChatMessage>().AsNoTracking()
                           join author in context.Set<User>().AsNoTracking()
                               on message.AuthorUserId equals author.Id
                           where message.SessionId == sessionId
                           orderby message.CreatedAt descending
                           select new ChatLine(
                               message.Id,
                               author.Id,
                               author.Username,
                               message.Body,
                               message.Voice,
                               message.CreatedAt))
            .Take(limit)
            .ToListAsync(cancellationToken);

        // Read newest-first so the limit takes the most recent, then reversed for display.
        lines.Reverse();

        return lines;
    }
}
