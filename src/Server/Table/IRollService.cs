namespace Vtt.Server.Table;

/// <summary>A roll as one particular recipient is entitled to see it.</summary>
public sealed record RollLine(
    Guid Id,
    Guid RollerUserId,
    string RollerUsername,
    string Expression,
    IReadOnlyList<int> Kept,
    IReadOnlyList<int> Dropped,
    int Modifier,
    int Total,
    RollVisibility Visibility,
    DateTimeOffset CreatedAt);

/// <summary>A roll, and exactly who is to be told about it.</summary>
/// <remarks>
/// The recipient list is computed on the server and the caller sends to those accounts and nobody
/// else. This is the shape task 062 will reuse for fog of war, which is most of why it exists.
/// </remarks>
public sealed record RollBroadcast(RollLine Line, IReadOnlyList<Guid> Recipients);

public interface IRollService
{
    /// <summary>Rolls, records, and works out who may hear about it. Null if refused.</summary>
    Task<RollBroadcast?> RollAsync(
        Guid sessionId,
        Guid rollerUserId,
        string expression,
        RollVisibility visibility,
        CancellationToken cancellationToken = default);

    /// <summary>Past rolls this caller is entitled to see. Null if they may not be at the table.</summary>
    Task<IReadOnlyList<RollLine>?> HistoryAsync(
        Guid sessionId,
        Guid callerId,
        int limit = 200,
        CancellationToken cancellationToken = default);
}
