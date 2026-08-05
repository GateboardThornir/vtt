using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Vtt.Server.Infrastructure;

namespace Vtt.Server.Accounts;

/// <summary>
/// Creates an account from the command line, bypassing the invite requirement.
/// </summary>
/// <remarks>
/// This exists to break a circle: registration needs an invite, an invite needs an account to have
/// issued it, and a fresh database has neither. It is meant to be run once, by the maintainer, with
/// shell access to the server. See ADR 008.
/// <para>
/// The account is created <see cref="AccountState.Active"/> — it is the one account that never went
/// through an invite and never needed approving, because there was nobody to approve it. It is not
/// marked as an administrator, because until task 016 the schema has no way to express that.
/// </para>
/// </remarks>
public static class CreateAccountCommand
{
    public const string Verb = "create-account";

    /// <summary>
    /// Whether these process arguments are asking for this command.
    /// </summary>
    /// <remarks>
    /// Matched exactly, and on nothing else. <c>Program.cs</c> is executed by three different
    /// things — the server, <c>dotnet ef</c> building the model at design time, and the integration
    /// tests through <c>WebApplicationFactory</c> — and a branch that triggered on the wrong
    /// arguments would break the migration tooling or the test suite in a way that looks entirely
    /// unrelated to its cause.
    /// </remarks>
    public static bool Matches(string[] args) =>
        args.Length > 0 && string.Equals(args[0], Verb, StringComparison.Ordinal);

    public static async Task<int> RunAsync(
        IServiceProvider services,
        string[] args,
        TextWriter output,
        Func<string, string?> readSecret,
        CancellationToken cancellationToken = default)
    {
        if (args.Length != 2)
        {
            await output.WriteLineAsync($"usage: {Verb} <username>");

            return 1;
        }

        var username = args[1];

        if (!RegistrationRules.IsWellFormedUsername(username))
        {
            await output.WriteLineAsync(
                $"'{username}' is not a valid username: {RegistrationRules.UsernameMinLength}-"
                + $"{RegistrationRules.UsernameMaxLength} characters, letters, digits, '-' and '_'.");

            return 1;
        }

        var password = readSecret("Password: ");
        var confirmation = readSecret("Confirm:  ");

        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            await output.WriteLineAsync("The passwords did not match.");

            return 1;
        }

        if (!RegistrationRules.IsAcceptablePassword(password))
        {
            await output.WriteLineAsync(
                $"The password must be at least {RegistrationRules.PasswordMinLength} characters.");

            return 1;
        }

        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<VttDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var user = User.CreateActive(username, passwords.Hash(password!), clock.GetUtcNow());

        context.Set<User>().Add(user);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await output.WriteLineAsync($"An account named '{username}' already exists.");

            return 1;
        }

        await output.WriteLineAsync($"Created account '{user.Username}' ({user.State}).");

        return 0;
    }
}
