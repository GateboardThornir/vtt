namespace Vtt.Server.Accounts;

/// <summary>What happened when someone tried to sign in.</summary>
public enum SignInOutcome
{
    Succeeded,

    /// <summary>
    /// No such account, or the wrong password. Deliberately one value for both.
    /// </summary>
    /// <remarks>
    /// Distinguishing them would turn the login form into a way to discover who has an account
    /// here, which on an invitation-only platform is exactly the membership list.
    /// </remarks>
    InvalidCredentials,

    /// <summary>Correct password, but an administrator has not approved the account yet.</summary>
    /// <remarks>
    /// Safe to disclose, and only reachable after the password has been verified: whoever gets this
    /// answer has already proved the account is theirs.
    /// </remarks>
    AwaitingApproval,

    /// <summary>Correct password, but the account has been disabled.</summary>
    Disabled,
}
