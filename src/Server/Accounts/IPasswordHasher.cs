namespace Vtt.Server.Accounts;

/// <summary>
/// Turns passwords into stored hashes, and checks them again.
/// </summary>
/// <remarks>
/// The only place in the codebase that handles a plaintext password. Implementations must never
/// log, transmit or persist either the password or the hash outside this boundary.
/// </remarks>
public interface IPasswordHasher
{
    /// <summary>Produces a hash suitable for storing in <c>users.password_hash</c>.</summary>
    string Hash(string password);

    /// <summary>Checks <paramref name="password"/> against a previously stored hash.</summary>
    PasswordVerification Verify(string password, string hash);
}
