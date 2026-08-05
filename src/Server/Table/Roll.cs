namespace Vtt.Server.Table;

/// <summary>Who may see a roll.</summary>
/// <remarks>
/// <see cref="Private"/> includes the Master: a player rolling privately is hiding from the other
/// players, not from the person running the game. That is what private means at a table.
/// </remarks>
public enum RollVisibility
{
    Public,
    Private,
    MasterOnly,
}

/// <summary>A roll that happened at a table, kept with what it was allowed to be seen by.</summary>
public class Roll
{
    public const int ExpressionMaxLength = 40;

    private Roll()
    {
    }

    public Guid Id { get; private set; }

    public Guid SessionId { get; private set; }

    public Guid RollerUserId { get; private set; }

    public string Expression { get; private set; } = null!;

    /// <summary>The kept faces, comma-separated. Small, ordered, and never queried into.</summary>
    public string Kept { get; private set; } = null!;

    public string Dropped { get; private set; } = null!;

    public int Modifier { get; private set; }

    public int Total { get; private set; }

    public RollVisibility Visibility { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Roll Record(
        Guid sessionId,
        Guid rollerUserId,
        Dice.RollResult result,
        RollVisibility visibility,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            SessionId = sessionId,
            RollerUserId = rollerUserId,
            Expression = result.Expression,
            Kept = string.Join(',', result.Kept),
            Dropped = string.Join(',', result.Dropped),
            Modifier = result.Modifier,
            Total = result.Total,
            Visibility = visibility,
            CreatedAt = now,
        };
}
