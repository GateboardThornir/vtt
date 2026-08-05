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

    /// <summary>Platform-level role. Read per request; never carried in the session cookie.</summary>
    public PlatformRole Role { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Moves the account to a new state on an administrator's instruction.
    /// </summary>
    /// <remarks>
    /// Approving and rejecting are the same operation with different destinations, so they share
    /// one guard. Returns false for a transition that is not allowed rather than throwing: an
    /// administrator clicking a stale button is an ordinary outcome, not an exceptional one.
    /// </remarks>
    public bool TransitionTo(AccountState state)
    {
        var allowed = (State, state) switch
        {
            (AccountState.Pending, AccountState.Active) => true,      // approve
            (AccountState.Pending, AccountState.Disabled) => true,    // reject
            (AccountState.Active, AccountState.Disabled) => true,     // disable
            (AccountState.Disabled, AccountState.Active) => true,     // re-enable
            _ => false,
        };

        if (allowed)
        {
            State = state;
        }

        return allowed;
    }

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
    /// Replaces the stored hash with one produced by the current work factor.
    /// </summary>
    /// <remarks>
    /// Called during sign-in when the framework reports the stored hash is out of date. It takes a
    /// hash, never a password: this type has no business knowing how hashing works.
    /// </remarks>
    public void ReplacePasswordHash(string passwordHash) => PasswordHash = passwordHash;

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

        // The bootstrap account is the administrator. ADR 008 noted that it could not be marked as
        // one because the schema had no way to say it; task 014 gave it one, and this closes that
        // gap. It also makes explicit what was already true: shell access is platform ownership.
        user.Role = PlatformRole.Admin;

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
