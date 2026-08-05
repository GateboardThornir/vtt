namespace Vtt.Server.Accounts;

/// <summary>
/// Why a registration was refused, as a stable code rather than a sentence.
/// </summary>
/// <remarks>
/// A code so the client at task 017 can translate it — the interface is bilingual, and a English
/// sentence from the server would arrive untranslatable.
/// </remarks>
public sealed record RegistrationError(string Error);
