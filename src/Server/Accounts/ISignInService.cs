namespace Vtt.Server.Accounts;

/// <summary>The result of a successful sign-in: who it was.</summary>
public sealed record SignedInUser(Guid Id, string Username);

public interface ISignInService
{
    /// <summary>
    /// Checks credentials and reports whether this account may sign in.
    /// </summary>
    /// <remarks>
    /// Answers <em>who you are</em> and nothing about what you may do. Authorisation is task 016's,
    /// and deliberately not decided here.
    /// </remarks>
    Task<(SignInOutcome Outcome, SignedInUser? User)> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);
}
