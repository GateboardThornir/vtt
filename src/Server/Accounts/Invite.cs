namespace Vtt.Server.Accounts;

/// <summary>
/// A single-use, expiring permission to register an account.
/// </summary>
/// <remarks>
/// The row holds only the hash of the token. The token itself exists in the response to whoever
/// issued it and in whatever they paste into a message, and nowhere else — so a leaked database,
/// dump or backup cannot be turned into working invitations for a platform whose entire access
/// control is "you must have been invited".
/// </remarks>
public class Invite
{
    /// <summary>How long a newly issued invite remains usable.</summary>
    /// <remarks>
    /// A constant rather than configuration: fewer than fifty people will ever hold one of these,
    /// and a knob nobody turns is a knob that rots. Invites are delivered by hand over a messaging
    /// app, so a week is long enough to be forgiving and short enough that a forgotten one lapses.
    /// </remarks>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private Invite()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Hash of the token, per <see cref="InviteToken.Hash"/>. Never the token itself.</summary>
    public string TokenHash { get; private set; } = null!;

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>When the invite was spent, or null while it is still unused.</summary>
    /// <remarks>
    /// A timestamp and an account rather than a boolean: "who used this, and when" is the question
    /// anyone actually asks of this table, and a <c>bool</c> cannot answer it.
    /// </remarks>
    public DateTimeOffset? ConsumedAt { get; private set; }

    public Guid? ConsumedByUserId { get; private set; }

    public static Invite Issue(string tokenHash, Guid createdByUserId, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            TokenHash = tokenHash,
            CreatedByUserId = createdByUserId,
            CreatedAt = now,
            ExpiresAt = now + Lifetime,
        };
}
