using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Vtt.Server.Accounts;

/// <summary>
/// Generates high-entropy secrets and reduces them to the form the database stores.
/// </summary>
/// <remarks>
/// Shared by invite tokens (011) and recovery codes (015). Task 011 refused to generalise from a
/// single example; with two it is clear which part is actually the same problem — generating and
/// hashing — and which part only rhymes. The entities, lifetimes and redemption rules stay
/// separate, because they differ in ways that matter.
/// </remarks>
public static class SecureToken
{
    /// <summary>Bytes of entropy behind each token.</summary>
    /// <remarks>
    /// 256 bits. Guessing one is not an attack anybody mounts, which is what lets the stored form
    /// be a single fast hash rather than a deliberately slow one — see ADR 007.
    /// </remarks>
    public const int TokenBytes = 32;

    /// <summary>Characters in the value <see cref="Hash"/> returns.</summary>
    public const int HashLength = 64;

    /// <summary>
    /// Produces a new secret. Cryptographically random, and safe to put in a URL unescaped.
    /// </summary>
    /// <remarks>
    /// <see cref="RandomNumberGenerator"/> rather than <see cref="Random"/>, which is seeded
    /// predictably and is not built to resist anyone trying to predict it. A <see cref="Guid"/> is
    /// equally wrong: version 4 carries 122 bits with no guarantee of cryptographic quality, and
    /// the version 7 used for primary keys deliberately encodes a timestamp, so part of it is
    /// predictable by construction.
    /// <para>
    /// Base64url rather than base64: the standard alphabet contains <c>+</c>, <c>/</c> and <c>=</c>,
    /// all of which mean something else inside a URL.
    /// </para>
    /// </remarks>
    public static string Generate() =>
        Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));

    /// <summary>
    /// Reduces a secret to its stored form.
    /// </summary>
    /// <remarks>
    /// SHA-256, deliberately, and not the password hasher. A password needs a slow hash because
    /// people choose passwords out of a very small space and an attacker holding the database will
    /// work through it; a value from <see cref="Generate"/> has no such space to search. The only
    /// property needed here is that the stored value cannot be turned back into the secret, and one
    /// pass of SHA-256 gives exactly that at no cost to every redemption. ADR 007.
    /// </remarks>
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
