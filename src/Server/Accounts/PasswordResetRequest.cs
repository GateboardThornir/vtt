namespace Vtt.Server.Accounts;

public sealed record PasswordResetRequest(string? Code, string? NewPassword);
