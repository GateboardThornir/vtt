namespace Vtt.Server.Accounts;

/// <summary>
/// What a username and a password must look like to be accepted at the request boundary.
/// </summary>
/// <remarks>
/// Policy, not storage. <see cref="User"/> caps the column length so a value cannot be truncated;
/// these rules decide what the platform is willing to accept in the first place, and they are
/// applied at the endpoint per <c>.claude/rules/backend.md</c>.
/// </remarks>
public static class RegistrationRules
{
    public const int UsernameMinLength = 3;

    public const int UsernameMaxLength = User.UsernameMaxLength;

    /// <summary>
    /// Shortest acceptable password.
    /// </summary>
    /// <remarks>
    /// Length is the only rule. No required symbol, digit or mixed case: composition rules are the
    /// standard example of guidance that backfired, because they reliably produce <c>Password1!</c>
    /// — a password that satisfies every rule and is among the first an attacker tries. Twelve
    /// characters of anything beats eight characters of theatre.
    /// </remarks>
    public const int PasswordMinLength = 12;

    /// <summary>
    /// Accepts a username of ASCII letters, digits, hyphen and underscore.
    /// </summary>
    /// <remarks>
    /// ASCII on purpose. Task 010 noted that two usernames can look identical while differing in
    /// code points — Cyrillic 'а' against Latin 'a' — which would let someone register a name
    /// indistinguishable from another's. Restricting the alphabet removes the problem instead of
    /// trying to detect it, at the cost of excluding names this group is unlikely to want.
    /// <para>
    /// Written as an explicit check rather than a regular expression so the length bounds come from
    /// the constants above and cannot drift out of step with them.
    /// </para>
    /// </remarks>
    public static bool IsWellFormedUsername(string? username) =>
        username is { Length: >= UsernameMinLength and <= UsernameMaxLength } &&
        username.All(IsAllowedInUsername);

    public static bool IsAcceptablePassword(string? password) =>
        password is not null && password.Length >= PasswordMinLength;

    private static bool IsAllowedInUsername(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '-' or '_';
}
