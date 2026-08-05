namespace Vtt.Server.Accounts;

/// <summary>Who the caller is. Carries no credential and no permission.</summary>
public sealed record SessionResponse(Guid Id, string Username);
