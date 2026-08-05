namespace Vtt.Server.Accounts;

/// <summary>
/// Where an account sits in its lifecycle.
/// </summary>
/// <remarks>
/// Stored as text rather than as an integer. ADR 004's argument was that this database gets read by
/// hand, and a <c>state</c> column containing <c>1</c> throws that away. The price is that renaming
/// a value becomes a data migration, so these three names are chosen as though permanent.
/// </remarks>
public enum AccountState
{
    /// <summary>Registered through an invite, waiting for an administrator. Cannot authenticate.</summary>
    Pending,

    /// <summary>Approved. The only state that may sign in.</summary>
    Active,

    /// <summary>Deactivated by an administrator. Kept rather than deleted, so the account's history stays intact.</summary>
    Disabled,
}
