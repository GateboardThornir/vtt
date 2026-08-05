using Microsoft.EntityFrameworkCore;
using Vtt.Server.Infrastructure;

namespace Vtt.Server.Accounts;

internal sealed class RecoveryService(
    VttDbContext context,
    IPasswordHasher passwords,
    TimeProvider clock) : IRecoveryService
{
    public async Task<IssuedRecoveryCode?> IssueAsync(
        Guid userId,
        Guid issuedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (!await context.Set<User>().AnyAsync(user => user.Id == userId, cancellationToken))
        {
            return null;
        }

        var code = SecureToken.Generate();
        var record = RecoveryCode.Issue(SecureToken.Hash(code), userId, issuedByUserId, clock.GetUtcNow());

        context.Set<RecoveryCode>().Add(record);
        await context.SaveChangesAsync(cancellationToken);

        // The one moment the plaintext exists. The administrator reads it, passes it on out of
        // band, and never learns the password the holder chooses with it.
        return new IssuedRecoveryCode(code, record.ExpiresAt);
    }

    public async Task<RecoveryOutcome> RedeemAsync(
        string code,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        if (!RegistrationRules.IsAcceptablePassword(newPassword))
        {
            return RecoveryOutcome.PasswordUnacceptable;
        }

        var now = clock.GetUtcNow();
        var hash = SecureToken.Hash(code);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // Same shape as spending an invite: one conditional statement claims the code, so two
        // simultaneous redemptions cannot both proceed to set a password.
        var claimed = await context.Set<RecoveryCode>()
            .Where(record => record.CodeHash == hash && record.UsedAt == null && record.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(record => record.UsedAt, now),
                cancellationToken);

        if (claimed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);

            return RecoveryOutcome.CodeInvalid;
        }

        var userId = await context.Set<RecoveryCode>()
            .Where(record => record.CodeHash == hash)
            .Select(record => record.UserId)
            .SingleAsync(cancellationToken);

        var user = await context.Set<User>().SingleAsync(candidate => candidate.Id == userId, cancellationToken);

        // The password only. A recovery code restores access to the account's credentials and
        // changes nothing else: a disabled account stays disabled, and a member stays a member.
        user.ReplacePasswordHash(passwords.Hash(newPassword));

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return RecoveryOutcome.PasswordChanged;
    }
}
