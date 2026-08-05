namespace Vtt.Server.Accounts;

/// <summary>
/// A freshly issued invite, including the only copy of its token that will ever exist.
/// </summary>
/// <remarks>
/// <paramref name="Token"/> is not stored anywhere — the database holds only its hash — so if it is
/// lost between here and the person being invited, the invite is dead and a new one must be issued.
/// That is the intended consequence of storing only the hash, and the UI at task 017 has to present
/// it that way rather than as an error.
/// </remarks>
public sealed record IssuedInvite(Guid Id, string Token, DateTimeOffset ExpiresAt);
