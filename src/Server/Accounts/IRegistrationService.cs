namespace Vtt.Server.Accounts;

public interface IRegistrationService
{
    /// <summary>
    /// Spends an invite and creates a <see cref="AccountState.Pending"/> account, or neither.
    /// </summary>
    /// <remarks>
    /// Assumes the username and password have already been validated at the boundary. Creating the
    /// account and consuming the invite happen together or not at all.
    /// </remarks>
    Task<RegistrationOutcome> RegisterAsync(
        string token,
        string username,
        string password,
        CancellationToken cancellationToken = default);
}
