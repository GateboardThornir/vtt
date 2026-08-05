using System.Collections.Concurrent;

namespace Vtt.Server.Table;

/// <summary>
/// Who is connected to which table, right now.
/// </summary>
/// <remarks>
/// In memory and deliberately so: this is presence, not state. It is worthless after a restart and
/// nothing should be recovered from it — the table's actual state belongs to the actor at task 060
/// and to the event log at 063.
/// <para>
/// One person may have several connections open — a second tab, a phone — so joining and leaving
/// are counted per account rather than per connection. Otherwise closing one tab would announce
/// that somebody had left while they were still sitting at the table.
/// </para>
/// </remarks>
public interface ITableParticipants
{
    IReadOnlyList<Participant> Join(Guid sessionId, string connectionId, Participant participant);

    Participant? Leave(Guid sessionId, string connectionId);

    /// <summary>How many live connections this account has to this session.</summary>
    int CountFor(Guid sessionId, Guid userId);

    IReadOnlyList<Guid> SessionsOf(string connectionId);

    IReadOnlyList<Participant> InSession(Guid sessionId);
}

internal sealed class TableParticipants : ITableParticipants
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, Participant>> _bySession = new();

    public IReadOnlyList<Participant> Join(Guid sessionId, string connectionId, Participant participant)
    {
        var connections = _bySession.GetOrAdd(sessionId, _ => new ConcurrentDictionary<string, Participant>());

        // Snapshot before adding, so the caller is told who was already here rather than being
        // told about themselves.
        var alreadyHere = Distinct(connections);

        connections[connectionId] = participant;

        return alreadyHere;
    }

    public Participant? Leave(Guid sessionId, string connectionId)
    {
        if (!_bySession.TryGetValue(sessionId, out var connections))
        {
            return null;
        }

        return connections.TryRemove(connectionId, out var participant) ? participant : null;
    }

    public int CountFor(Guid sessionId, Guid userId) =>
        _bySession.TryGetValue(sessionId, out var connections)
            ? connections.Values.Count(participant => participant.UserId == userId)
            : 0;

    public IReadOnlyList<Guid> SessionsOf(string connectionId) =>
        [.. _bySession.Where(entry => entry.Value.ContainsKey(connectionId)).Select(entry => entry.Key)];

    public IReadOnlyList<Participant> InSession(Guid sessionId) =>
        _bySession.TryGetValue(sessionId, out var connections) ? Distinct(connections) : [];

    private static List<Participant> Distinct(ConcurrentDictionary<string, Participant> connections) =>
        [.. connections.Values.DistinctBy(participant => participant.UserId)];
}

public static class TableServices
{
    public static IServiceCollection AddTable(this IServiceCollection services) =>
        services
            .AddScoped<ITableAccess, TableAccess>()
            .AddScoped<IChatService, ChatService>()
            .AddScoped<IRollService, RollService>()

            // Singleton because presence is process-wide, and in memory because it is worthless
            // after a restart.
            .AddSingleton<ITableParticipants, TableParticipants>();
}
