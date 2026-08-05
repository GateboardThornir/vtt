namespace Vtt.Server.Sessions;

/// <summary>Where a session is in its life. Forward only.</summary>
public enum SessionState
{
    Planned,
    Open,
    Closed,
}
