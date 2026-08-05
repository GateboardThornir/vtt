using Microsoft.EntityFrameworkCore;
using Vtt.Server.Infrastructure;

namespace Vtt.Server.Accounts;

internal sealed class InviteService(VttDbContext context, TimeProvider clock) : IInviteService
{
    public async Task<IssuedInvite> IssueAsync(
        Guid createdByUserId,
        CancellationToken cancellationToken = default)
    {
        var token = SecureToken.Generate();
        var invite = Invite.Issue(SecureToken.Hash(token), createdByUserId, clock.GetUtcNow());

        context.Set<Invite>().Add(invite);
        await context.SaveChangesAsync(cancellationToken);

        // The one and only time the plaintext leaves this method. It is deliberately not logged.
        return new IssuedInvite(invite.Id, token, invite.ExpiresAt);
    }

    public async Task<InviteStatus> ValidateAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var invite = await FindAsync(token, cancellationToken);

        return Classify(invite, clock.GetUtcNow());
    }

    public async Task<InviteStatus> ConsumeAsync(
        string token,
        Guid consumedByUserId,
        CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();

        // The whole design of this method is in this one statement. Reading the invite, checking it
        // is unspent, and then writing it back would leave a window between the read and the write
        // in which a second redemption passes the same check — two accounts from one invite, which
        // defeats the point of an invite-only platform. A single conditional UPDATE has no such
        // window: PostgreSQL serialises the row, and exactly one caller sees a row affected.
        var affected = await context.Set<Invite>()
            .Where(invite =>
                invite.TokenHash == SecureToken.Hash(token) &&
                invite.ConsumedAt == null &&
                invite.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(invite => invite.ConsumedAt, now)
                    .SetProperty(invite => invite.ConsumedByUserId, consumedByUserId),
                cancellationToken);

        if (affected > 0)
        {
            return InviteStatus.Ok;
        }

        // Nothing was updated, so this call lost. Reading now only decides *why*, for the caller's
        // message — it cannot reintroduce the race, because the update has already been decided.
        return Classify(await FindAsync(token, cancellationToken), now) switch
        {
            // The row looks usable but the update matched nothing: another caller consumed it
            // between the two statements.
            InviteStatus.Ok => InviteStatus.AlreadyConsumed,
            var status => status,
        };
    }

    private Task<Invite?> FindAsync(string token, CancellationToken cancellationToken) =>
        context.Set<Invite>()
            .AsNoTracking()
            .SingleOrDefaultAsync(invite => invite.TokenHash == SecureToken.Hash(token), cancellationToken);

    private static InviteStatus Classify(Invite? invite, DateTimeOffset now) => invite switch
    {
        null => InviteStatus.NotFound,
        { ConsumedAt: not null } => InviteStatus.AlreadyConsumed,
        _ when invite.ExpiresAt <= now => InviteStatus.Expired,
        _ => InviteStatus.Ok,
    };
}
