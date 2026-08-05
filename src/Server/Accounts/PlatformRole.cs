namespace Vtt.Server.Accounts;

/// <summary>
/// What an account may do at the platform level.
/// </summary>
/// <remarks>
/// Two values, deliberately, and not a permission system. Anything finer — per-capability grants,
/// several kinds of administrator — is machinery for a platform with more users than this one will
/// ever have. When a third value is wanted, that is a design conversation and not a schema change.
/// <para>
/// Being an <see cref="Admin"/> grants **no** access to any campaign's content, per
/// <c>.claude/rules/security.md</c>. Platform administration and campaign membership are unrelated,
/// and the moment one implies the other the visibility rules have a hole.
/// </para>
/// </remarks>
public enum PlatformRole
{
    Member,
    Admin,
}
