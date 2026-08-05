using Microsoft.AspNetCore.Authorization;

namespace Vtt.Server.Accounts;

/// <summary>
/// The platform-level authorisation policies, by name.
/// </summary>
/// <remarks>
/// Declared on endpoints rather than checked inside handlers, and the difference matters: an
/// endpoint with no policy is visibly unprotected, whereas an endpoint that forgot to call a guard
/// looks exactly like one that remembered.
/// </remarks>
public static class AccountPolicies
{
    /// <summary>Signed in, and the account is still <see cref="AccountState.Active"/>.</summary>
    public const string ActiveAccount = "ActiveAccount";

    /// <summary>Active, and a platform administrator.</summary>
    public const string Administrator = "Administrator";
}

/// <summary>Requires the caller's account to be active, and optionally an administrator.</summary>
public sealed class AccountRequirement(bool mustBeAdministrator) : IAuthorizationRequirement
{
    public bool MustBeAdministrator { get; } = mustBeAdministrator;
}
