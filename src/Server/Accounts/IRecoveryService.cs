namespace Vtt.Server.Accounts;

/// <summary>A freshly issued recovery code — the only copy of its plaintext that will exist.</summary>
public sealed record IssuedRecoveryCode(string Code, DateTimeOffset ExpiresAt);

public enum RecoveryOutcome
{
    PasswordChanged,

    /// <summary>Unknown, expired or already used. One value, on purpose.</summary>
    /// <remarks>
    /// The redemption endpoint is unauthenticated. Unlike an invite — where the holder of a real
    /// token benefits from knowing it merely lapsed — a recovery code identifies a specific
    /// account, so distinguishing "expired" from "no such code" would confirm to a stranger that a
    /// code once existed for someone.
    /// </remarks>
    CodeInvalid,

    /// <summary>The new password does not meet the length rule.</summary>
    PasswordUnacceptable,
}

public interface IRecoveryService
{
    Task<IssuedRecoveryCode?> IssueAsync(
        Guid userId,
        Guid issuedByUserId,
        CancellationToken cancellationToken = default);

    Task<RecoveryOutcome> RedeemAsync(
        string code,
        string newPassword,
        CancellationToken cancellationToken = default);
}
