namespace Vtt.Server.Accounts;

/// <summary>
/// The outcome of checking a password against a stored hash.
/// </summary>
/// <remarks>
/// Three-valued rather than a boolean because the hasher can tell us a stored hash was produced
/// with an older work factor. Nothing acts on that until task 013, but a two-valued API could not
/// carry the signal at all, and the caller that eventually needs it is the only one holding the
/// plaintext password — the one moment a rehash is possible.
/// </remarks>
public enum PasswordVerification
{
    /// <summary>Wrong password, or a hash that could not be read.</summary>
    Failed,

    /// <summary>Correct password.</summary>
    Success,

    /// <summary>Correct password, but the stored hash should be replaced with a stronger one.</summary>
    SuccessButNeedsRehash,
}
