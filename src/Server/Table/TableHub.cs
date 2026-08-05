using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Vtt.Server.Accounts;

namespace Vtt.Server.Table;

/// <summary>
/// The live channel for one session's table.
/// </summary>
/// <remarks>
/// Deliberately thin: it authenticates, joins, leaves and broadcasts. It holds no game state —
/// that belongs to the table actor at task 060, and a hub that accumulates state is a hub that
/// cannot be replaced by one.
/// <para>
/// Authentication comes from the same session cookie the HTTP API uses: the browser sends it on the
/// WebSocket handshake, so nothing new is issued and nothing new can leak.
/// </para>
/// </remarks>
[Authorize(Policy = AccountPolicies.ActiveAccount)]
public sealed class TableHub(
    ITableAccess access,
    ITableParticipants participants,
    IChatService chat,
    IRollService rolls) : Hub<ITableClient>
{
    /// <summary>
    /// Joins the caller to a session's table.
    /// </summary>
    /// <remarks>
    /// Authorisation happens here rather than at connect: a connection proves who you are, and a
    /// group proves what you may see. A caller who is not on the campaign's roster, or whose
    /// session is not open, is refused and told nothing further.
    /// </remarks>
    [HubMethodName("JoinSession")]
    public async Task<bool> JoinSessionAsync(Guid sessionId)
    {
        var userId = SessionCookie.UserIdOf(Context.User!);

        if (userId is null)
        {
            return false;
        }

        var participant = await access.AdmitAsync(sessionId, userId.Value);

        if (participant is null)
        {
            return false;
        }

        // Derived from the session id, never taken from the caller.
        await Groups.AddToGroupAsync(Context.ConnectionId, TableGroups.ForSession(sessionId));

        var alreadyHere = participants.Join(sessionId, Context.ConnectionId, participant);

        await Clients.Caller.Participants(alreadyHere);

        var history = await chat.HistoryAsync(sessionId, participant.UserId);

        if (history is not null)
        {
            await Clients.Caller.ChatHistory(history);
        }

        var pastRolls = await rolls.HistoryAsync(sessionId, participant.UserId);

        if (pastRolls is not null)
        {
            // Already filtered for this caller: a reconnecting player never learns of a roll that
            // was hidden from them while they were away.
            await Clients.Caller.RollHistory(pastRolls);
        }

        if (participants.CountFor(sessionId, participant.UserId) == 1)
        {
            // Announced once per person, not once per tab.
            await Clients.OthersInGroup(TableGroups.ForSession(sessionId))
                .ParticipantJoined(participant);
        }

        return true;
    }

    /// <summary>Rolls dice at the table.</summary>
    /// <remarks>
    /// Sent to the accounts the service says may see it, never to the group. Broadcasting to
    /// everyone and letting clients hide what they should not have is the failure mode
    /// <c>.claude/rules/security.md</c> exists to forbid, and SignalR makes it the easy path.
    /// </remarks>
    [HubMethodName("Roll")]
    public async Task<bool> RollAsync(Guid sessionId, string expression, RollVisibility visibility)
    {
        var userId = SessionCookie.UserIdOf(Context.User!);

        if (userId is null)
        {
            return false;
        }

        var broadcast = await rolls.RollAsync(sessionId, userId.Value, expression, visibility);

        if (broadcast is null)
        {
            return false;
        }

        await Clients.Users([.. broadcast.Recipients.Select(id => id.ToString())]).Rolled(broadcast.Line);

        return true;
    }

    /// <summary>Says something at the table.</summary>
    /// <remarks>
    /// Admission is re-checked inside the service on every call. A client that stays in the group
    /// after losing its place on the roster gets nothing back and broadcasts nothing.
    /// </remarks>
    [HubMethodName("Say")]
    public async Task<bool> SayAsync(Guid sessionId, string body, ChatVoice voice)
    {
        var userId = SessionCookie.UserIdOf(Context.User!);

        if (userId is null)
        {
            return false;
        }

        var line = await chat.SayAsync(sessionId, userId.Value, body, voice);

        if (line is null)
        {
            return false;
        }

        await Clients.Group(TableGroups.ForSession(sessionId)).ChatSaid(line);

        return true;
    }

    /// <remarks>
    /// The C# name carries the suffix the naming rule wants; the attribute keeps the wire name the
    /// clients actually call. A hub method name is a contract with every client, and renaming it to
    /// satisfy a local convention would be the convention deciding the protocol.
    /// </remarks>
    [HubMethodName("LeaveSession")]
    public async Task LeaveSessionAsync(Guid sessionId)
    {
        await RemoveAsync(sessionId, Context.ConnectionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var sessionId in participants.SessionsOf(Context.ConnectionId))
        {
            await RemoveAsync(sessionId, Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task RemoveAsync(Guid sessionId, string connectionId)
    {
        var departed = participants.Leave(sessionId, connectionId);

        if (departed is null)
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(connectionId, TableGroups.ForSession(sessionId));

        if (participants.CountFor(sessionId, departed.UserId) == 0)
        {
            await Clients.Group(TableGroups.ForSession(sessionId)).ParticipantLeft(departed);
        }
    }
}
