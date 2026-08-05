using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vtt.Server.Infrastructure;

namespace Vtt.Server.Accounts;

internal sealed class RegistrationService(
    VttDbContext context,
    IInviteService invites,
    IPasswordHasher passwords,
    TimeProvider clock) : IRegistrationService
{
    public async Task<RegistrationOutcome> RegisterAsync(
        string token,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        // Both writes must stick together. The order is not a preference: invites.consumed_by_user_id
        // references users, so the account has to exist before the invite can record who spent it —
        // but if the invite then turns out to be gone, the account must not survive. Without the
        // transaction the loser of a race leaves an orphaned account: a username held forever by a
        // row nobody can ever sign into, which is worse than a rejected registration.
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var user = User.Register(username, passwords.Hash(password), clock.GetUtcNow());
        context.Set<User>().Add(user);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUsernameCollision(exception))
        {
            // Checking for the name first and inserting afterwards would have a window between the
            // two statements. The unique index is the only thing that actually decides, so the
            // insert is attempted and its failure is read — the same conclusion task 010 reached.
            await transaction.RollbackAsync(cancellationToken);

            return RegistrationOutcome.UsernameTaken;
        }

        // Resolved from the same request scope, so it shares this DbContext and enrols in the
        // transaction above rather than opening its own.
        var status = await invites.ConsumeAsync(token, user.Id, cancellationToken);

        if (status != InviteStatus.Ok)
        {
            await transaction.RollbackAsync(cancellationToken);

            return status switch
            {
                InviteStatus.Expired => RegistrationOutcome.InviteExpired,
                InviteStatus.AlreadyConsumed => RegistrationOutcome.InviteAlreadyUsed,
                _ => RegistrationOutcome.InviteInvalid,
            };
        }

        await transaction.CommitAsync(cancellationToken);

        return RegistrationOutcome.Registered;
    }

    private static bool IsUsernameCollision(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
