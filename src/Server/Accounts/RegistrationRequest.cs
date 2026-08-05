namespace Vtt.Server.Accounts;

/// <summary>What a caller sends to register an account.</summary>
/// <remarks>
/// No email field, and none may ever be added. The password travels in this record and must not
/// appear in any log line, validation message or exception text derived from it.
/// </remarks>
public sealed record RegistrationRequest(string? Token, string? Username, string? Password);
