namespace Vtt.Server.Campaigns;

public enum RosterOutcome
{
    Done,

    /// <summary>The campaign does not exist, or the caller may not see it. One value, deliberately.</summary>
    NoSuchCampaign,

    /// <summary>The caller is on the roster but is not its Master.</summary>
    NotTheMaster,

    NoSuchAccount,

    /// <summary>The move is not legal from the current state — accepting twice, leaving as Master.</summary>
    NotAllowed,
}
