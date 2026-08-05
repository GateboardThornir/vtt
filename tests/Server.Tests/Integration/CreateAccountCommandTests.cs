using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vtt.Server.Accounts;
using Vtt.Server.Infrastructure;

namespace Vtt.Server.Tests.Integration;

/// <summary>
/// The bootstrap command, run against the real database.
/// </summary>
/// <remarks>
/// The command takes its output writer and its secret reader as parameters precisely so it can be
/// driven from a test without a terminal. The console wiring itself stays a thin uncovered edge.
/// </remarks>
[Collection(IntegrationDatabase.Name)]
[Trait("Category", "Integration")]
public class CreateAccountCommandTests(PostgresFixture fixture) : IAsyncLifetime
{
    private const string GoodPassword = "a perfectly ordinary passphrase";

    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ItCreatesAnActiveAccount()
    {
        var (exitCode, output) = await RunAsync(["create-account", "Mattia"], GoodPassword, GoodPassword);

        Assert.Equal(0, exitCode);
        Assert.Contains("Created account 'Mattia'", output, StringComparison.Ordinal);

        await using var scope = NewScope();
        var user = await scope.Context.Set<User>().SingleAsync();

        // Active, not Pending: this is the one account that never went through an invite and never
        // needed approving, because there was nobody to approve it.
        Assert.Equal(AccountState.Active, user.State);
        Assert.Equal("Mattia", user.Username);
    }

    [Fact]
    public async Task TheStoredPasswordIsHashedAndVerifiable()
    {
        await RunAsync(["create-account", "Mattia"], GoodPassword, GoodPassword);

        await using var scope = NewScope();
        var user = await scope.Context.Set<User>().SingleAsync();
        var hasher = scope.Provider.GetRequiredService<IPasswordHasher>();

        Assert.DoesNotContain(GoodPassword, user.PasswordHash, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(PasswordVerification.Success, hasher.Verify(GoodPassword, user.PasswordHash));
    }

    [Fact]
    public async Task MismatchedPasswordsCreateNothing()
    {
        var (exitCode, output) = await RunAsync(["create-account", "Mattia"], GoodPassword, "something else entirely");

        Assert.Equal(1, exitCode);
        Assert.Contains("did not match", output, StringComparison.Ordinal);
        await AssertNoAccounts();
    }

    [Fact]
    public async Task AShortPasswordCreatesNothing()
    {
        var (exitCode, _) = await RunAsync(["create-account", "Mattia"], "short", "short");

        Assert.Equal(1, exitCode);
        await AssertNoAccounts();
    }

    [Fact]
    public async Task AMalformedUsernameCreatesNothing()
    {
        var (exitCode, _) = await RunAsync(["create-account", "no spaces allowed"], GoodPassword, GoodPassword);

        Assert.Equal(1, exitCode);
        await AssertNoAccounts();
    }

    [Fact]
    public async Task ASecondAccountWithTheSameNameIsRefusedCleanly()
    {
        await RunAsync(["create-account", "Mattia"], GoodPassword, GoodPassword);

        var (exitCode, output) = await RunAsync(["create-account", "mattia"], GoodPassword, GoodPassword);

        Assert.Equal(1, exitCode);
        Assert.Contains("already exists", output, StringComparison.Ordinal);

        await using var scope = NewScope();
        Assert.Single(await scope.Context.Set<User>().ToListAsync());
    }

    [Fact]
    public async Task WithoutAUsernameItPrintsUsage()
    {
        var (exitCode, output) = await RunAsync(["create-account"], GoodPassword, GoodPassword);

        Assert.Equal(1, exitCode);
        Assert.Contains("usage:", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData()]
    [InlineData("serve")]
    [InlineData("createaccount")]
    [InlineData("--applicationName", "Vtt.Server")]
    public void OnlyTheExactVerbTriggersTheCommand(params string[] args)
    {
        // Program.cs is executed by the server, by `dotnet ef` at design time and by the test host.
        // A branch that fired on anything else would break the migration tooling or this very suite.
        Assert.False(CreateAccountCommand.Matches(args));
    }

    [Fact]
    public void TheExactVerbDoesTriggerIt() =>
        Assert.True(CreateAccountCommand.Matches(["create-account", "Mattia"]));

    private async Task<(int ExitCode, string Output)> RunAsync(string[] args, params string[] secrets)
    {
        var queue = new Queue<string>(secrets);
        await using var output = new StringWriter();

        var exitCode = await CreateAccountCommand.RunAsync(
            fixture.Factory.Services,
            args,
            output,
            _ => queue.Count > 0 ? queue.Dequeue() : null);

        return (exitCode, output.ToString());
    }

    private async Task AssertNoAccounts()
    {
        await using var scope = NewScope();

        Assert.Empty(await scope.Context.Set<User>().ToListAsync());
    }

    private Scope NewScope() => new(fixture.Factory.Services.CreateScope());

    private sealed class Scope(IServiceScope scope) : IAsyncDisposable
    {
        public IServiceProvider Provider { get; } = scope.ServiceProvider;

        public VttDbContext Context { get; } = scope.ServiceProvider.GetRequiredService<VttDbContext>();

        public ValueTask DisposeAsync()
        {
            scope.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
