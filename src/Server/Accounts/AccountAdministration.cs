using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Vtt.Server.Infrastructure;
using Vtt.Server.Notifications;

namespace Vtt.Server.Accounts;

internal sealed class AccountAdministration(VttDbContext context, INotificationService notifications)
    : IAccountAdministration
{
    public async Task<IReadOnlyList<AccountSummary>> PendingAsync(
        CancellationToken cancellationToken = default) =>
        await Summaries(account => account.State == AccountState.Pending).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AccountSummary>> AllAsync(
        CancellationToken cancellationToken = default) =>
        await Summaries(filter: null).ToListAsync(cancellationToken);

    public async Task<TransitionResult> SetStateAsync(
        Guid accountId,
        AccountState state,
        CancellationToken cancellationToken = default)
    {
        var user = await context.Set<User>()
            .SingleOrDefaultAsync(candidate => candidate.Id == accountId, cancellationToken);

        if (user is null)
        {
            return TransitionResult.NoSuchAccount;
        }

        if (!user.TransitionTo(state))
        {
            return TransitionResult.NotAllowed;
        }

        await context.SaveChangesAsync(cancellationToken);

        // The only way an applicant finds out. There is no email to send.
        if (state is AccountState.Active or AccountState.Disabled)
        {
            await notifications.RaiseAsync(
                accountId,
                state == AccountState.Active ? NotificationKind.AccountApproved : NotificationKind.AccountRejected,
                subject: null,
                cancellationToken);
        }

        return TransitionResult.Applied;
    }

    public Task<bool> IsAdministratorAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        context.Set<User>()
            .AnyAsync(
                user => user.Id == accountId &&
                        user.Role == PlatformRole.Admin &&
                        user.State == AccountState.Active,
                cancellationToken);

    /// <remarks>
    /// Filtering and ordering happen on the entity, and the projection comes last. The other order
    /// does not translate: EF cannot turn <c>OrderBy(x =&gt; new Summary(...).CreatedAt)</c> into
    /// SQL, because the object being ordered by does not exist in the database.
    /// <para>
    /// Projecting at all — rather than loading users and mapping them — means the password hash is
    /// never selected. The surest way for it not to reach a response is for it never to be read.
    /// </para>
    /// </remarks>
    private IQueryable<AccountSummary> Summaries(Expression<Func<User, bool>>? filter)
    {
        IQueryable<User> users = context.Set<User>().AsNoTracking();

        if (filter is not null)
        {
            users = users.Where(filter);
        }

        return users
            .OrderBy(user => user.CreatedAt)
            .Select(user => new AccountSummary(
                user.Id,
                user.Username,
                user.State,
                user.Role,
                user.CreatedAt));
    }
}
