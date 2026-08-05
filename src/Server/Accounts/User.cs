namespace Vtt.Server.Accounts;

/// <summary>
/// A platform account. Username and password only — there is no email field, by design.
/// </summary>
/// <remarks>
/// Setters are private and construction goes through <see cref="Register"/> because
/// <see cref="Username"/> and <see cref="UsernameNormalized"/> must never disagree: the normalised
/// column is what the unique index is built on, so a user created around the factory could
/// duplicate an existing account. One way in is what makes that impossible rather than merely
/// unlikely.
/// </remarks>
public class User
{
    /// <summary>Longest username the column accepts.</summary>
    /// <remarks>
    /// A storage bound, not a validation rule. Allowed characters and a minimum length belong at
    /// the request boundary in task 012, per <c>.claude/rules/backend.md</c>.
    /// </remarks>
    public const int UsernameMaxLength = 32;

    // EF materialises through this; application code must use Register.
    private User()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>The username as the user typed it, preserved for display.</summary>
    public string Username { get; private set; } = null!;

    /// <summary>Lower-cased <see cref="Username"/>. Carries the unique index.</summary>
    public string UsernameNormalized { get; private set; } = null!;

    public string PasswordHash { get; private set; } = null!;

    public AccountState State { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Creates a new account in <see cref="AccountState.Pending"/>.
    /// </summary>
    /// <remarks>
    /// <paramref name="createdAt"/> is supplied rather than read from the clock here: a hidden
    /// <c>DateTimeOffset.UtcNow</c> makes the type untestable, and passing it in avoids introducing
    /// a clock abstraction nothing else needs yet.
    /// <para>
    /// Pending is not a parameter. Every account starts there — an invite gets someone registered,
    /// an administrator decides whether they stay (task 014).
    /// </para>
    /// </remarks>
    public static User Register(string username, string passwordHash, DateTimeOffset createdAt) =>
        new()
        {
            // Version 7 is time-ordered, so inserts land at the end of the index instead of
            // scattering across it the way a random version 4 does.
            Id = Guid.CreateVersion7(),
            Username = username,
            UsernameNormalized = Normalize(username),
            PasswordHash = passwordHash,
            State = AccountState.Pending,
            CreatedAt = createdAt,
        };

    /// <summary>
    /// Creates an already-active account, bypassing both the invite and the approval.
    /// </summary>
    /// <remarks>
    /// The bootstrap path only, used by <c>create-account</c> to break the circle where
    /// registration needs an invite and an invite needs an account (ADR 008). A separate factory
    /// rather than an <c>Activate()</c> mutator, because approving a pending account is task 014's
    /// to design and this task should not hand it an API by accident.
    /// </remarks>
    public static User CreateActive(string username, string passwordHash, DateTimeOffset createdAt)
    {
        var user = Register(username, passwordHash, createdAt);
        user.State = AccountState.Active;

        return user;
    }

    /// <summary>
    /// Reduces a username to the form the unique index compares.
    /// </summary>
    /// <remarks>
    /// Invariant rather than culture-sensitive: in Turkish, lower-casing 'I' yields the dotless 'ı',
    /// so a culture-sensitive transform would make uniqueness depend on the server's locale.
    /// </remarks>
    public static string Normalize(string username) => username.ToLowerInvariant();
}
