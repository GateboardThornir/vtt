using Microsoft.EntityFrameworkCore;
using Vtt.Server.Infrastructure;

namespace Vtt.Server.Accounts;

internal sealed class SignInService(VttDbContext context, IPasswordHasher passwords) : ISignInService
{
    public async Task<(SignInOutcome Outcome, SignedInUser? User)> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var normalised = User.Normalize(username);

        var user = await context.Set<User>()
            .SingleOrDefaultAsync(candidate => candidate.UsernameNormalized == normalised, cancellationToken);

        if (user is null)
        {
            return (SignInOutcome.InvalidCredentials, null);
        }

        var verification = passwords.Verify(password, user.PasswordHash);

        if (verification == PasswordVerification.Failed)
        {
            return (SignInOutcome.InvalidCredentials, null);
        }

        // Password first, state second, and the order matters. Reporting "awaiting approval" before
        // checking the password would answer it for anyone who guessed a username, turning the
        // state message into a way to enumerate accounts. Everyone who reaches this line has
        // already proved the account is theirs, so telling them why they cannot get in is safe.
        if (user.State != AccountState.Active)
        {
            return (
                user.State == AccountState.Pending ? SignInOutcome.AwaitingApproval : SignInOutcome.Disabled,
                null);
        }

        if (verification == PasswordVerification.SuccessButNeedsRehash)
        {
            // The only moment the plaintext exists is the only moment the stored hash can be
            // upgraded. This is what lets the work factor rise over the years without anyone being
            // asked to change their password. The signal has existed since 010 with no consumer.
            user.ReplacePasswordHash(passwords.Hash(password));
            await context.SaveChangesAsync(cancellationToken);
        }

        return (SignInOutcome.Succeeded, new SignedInUser(user.Id, user.Username));
    }
}
