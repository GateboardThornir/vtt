namespace Vtt.Server.Table;

/// <summary>Who is connected to a table right now.</summary>
public sealed record Participant(Guid UserId, string Username);

/// <summary>
/// Everything the server can send a table client.
/// </summary>
/// <remarks>
/// A typed contract rather than magic strings, so 044 and the Phase 2 table engine extend one place
/// instead of inventing message names that drift apart. SignalR generates the dispatch from this.
/// </remarks>
public interface ITableClient
{
    Task ParticipantJoined(Participant participant);

    Task ParticipantLeft(Participant participant);

    /// <summary>The full list, sent to a client when it joins.</summary>
    Task Participants(IReadOnlyList<Participant> participants);

    /// <summary>Somebody said something.</summary>
    Task ChatSaid(ChatLine line);

    /// <summary>Recent history, sent to a client when it joins.</summary>
    Task ChatHistory(IReadOnlyList<ChatLine> lines);
}
