namespace Vtt.Server.Accounts;

/// <summary>
/// A single-use, expiring permission to choose a new password for one account.
/// </summary>
/// <remarks>
/// The platform holds no email address, so recovery is mediated by an administrator: they mint one
/// of these, hand the plaintext over in person or through a messaging app, and never learn what
/// password the holder subsequently chooses. Only the hash is stored.
/// </remarks>
public class RecoveryCode
{
    /// <summary>How long a newly issued code remains usable.</summary>
    /// <remarks>
    /// Much shorter than an invite's week. An invite is a scheduling convenience; this is a live
    /// credential for an existing account, travelling through a chat log that the platform does not
    /// control and cannot clean up. A short life is the only mitigation available for that.
    /// </remarks>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(2);

    private RecoveryCode()
    {
    }

    public Guid Id { get; private set; }

    public string CodeHash { get; private set; } = null!;

    /// <summary>The account this code can recover. Bound at issue, unlike an invite.</summary>
    public Guid UserId { get; private set; }

    public Guid IssuedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    public static RecoveryCode Issue(
        string codeHash,
        Guid userId,
        Guid issuedByUserId,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            CodeHash = codeHash,
            UserId = userId,
            IssuedByUserId = issuedByUserId,
            CreatedAt = now,
            ExpiresAt = now + Lifetime,
        };
}
