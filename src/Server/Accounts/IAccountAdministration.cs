namespace Vtt.Server.Accounts;

/// <summary>An account as an administrator sees it. No hash, ever.</summary>
public sealed record AccountSummary(
    Guid Id,
    string Username,
    AccountState State,
    PlatformRole Role,
    DateTimeOffset CreatedAt);

public enum TransitionResult
{
    Applied,
    NoSuchAccount,
    NotAllowed,
}

public interface IAccountAdministration
{
    /// <summary>Accounts waiting for a decision, oldest first.</summary>
    Task<IReadOnlyList<AccountSummary>> PendingAsync(CancellationToken cancellationToken = default);

    /// <summary>Every account, for the administration screen at 017.</summary>
    Task<IReadOnlyList<AccountSummary>> AllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves an account to a new state. Approve, reject, disable and re-enable are all this.
    /// </summary>
    Task<TransitionResult> SetStateAsync(
        Guid accountId,
        AccountState state,
        CancellationToken cancellationToken = default);

    /// <summary>Whether this account is a platform administrator, read fresh from the database.</summary>
    Task<bool> IsAdministratorAsync(Guid accountId, CancellationToken cancellationToken = default);
}
