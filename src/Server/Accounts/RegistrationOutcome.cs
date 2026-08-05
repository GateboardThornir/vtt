namespace Vtt.Server.Accounts;

/// <summary>What happened when someone tried to register.</summary>
public enum RegistrationOutcome
{
    Registered,

    /// <summary>The token was not recognised. Deliberately says no more than that.</summary>
    InviteInvalid,

    InviteExpired,

    InviteAlreadyUsed,

    UsernameTaken,
}
