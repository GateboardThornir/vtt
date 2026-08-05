using Microsoft.AspNetCore.Identity;

namespace Vtt.Server.Accounts;

/// <summary>
/// <see cref="IPasswordHasher"/> backed by the framework's own <see cref="PasswordHasher{TUser}"/>.
/// </summary>
/// <remarks>
/// Named for where the implementation comes from rather than for the algorithm it currently uses:
/// the whole point of delegating is that the algorithm and work factor are Microsoft's to raise as
/// hardware moves, and a name like <c>Pbkdf2PasswordHasher</c> would be a lie the first time they
/// do. Writing this by hand would be the worst decision available — see ADR 006.
/// <para>
/// Stateless and thread-safe, so it is registered as a singleton.
/// </para>
/// </remarks>
internal sealed class IdentityPasswordHasher : IPasswordHasher
{
    // PasswordHasher<TUser> is generic only so that callers can plug in a rehash policy per user
    // type; the built-in implementation never touches the instance, which is why null is passed
    // below. Verified by the unit tests rather than assumed.
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(null!, password);

    public PasswordVerification Verify(string password, string hash)
    {
        // A stored hash that cannot be parsed is a failed verification, not an exception: the only
        // caller is a login attempt, and a corrupt row must not become a 500 that distinguishes
        // that account from any other.
        PasswordVerificationResult result;

        try
        {
            result = _hasher.VerifyHashedPassword(null!, hash, password);
        }
        catch (FormatException)
        {
            return PasswordVerification.Failed;
        }

        return result switch
        {
            PasswordVerificationResult.Success => PasswordVerification.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordVerification.SuccessButNeedsRehash,
            _ => PasswordVerification.Failed,
        };
    }
}
