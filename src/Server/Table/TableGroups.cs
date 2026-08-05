namespace Vtt.Server.Table;

/// <summary>
/// Group names for the live table.
/// </summary>
/// <remarks>
/// Derived from the session identifier and never taken from the client. A group name supplied by a
/// caller is a way to join a table they were never admitted to, and SignalR will not second-guess
/// it.
/// <para>
/// One group per **session**, not per campaign: two sessions of the same campaign are two different
/// tables, and a closed session has no live audience at all.
/// </para>
/// </remarks>
internal static class TableGroups
{
    public static string ForSession(Guid sessionId) => $"session:{sessionId}";
}
