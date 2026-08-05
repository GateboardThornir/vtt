using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Vtt.Server.Accounts;
using Vtt.Server.Infrastructure;

namespace Vtt.Server.Tests.Integration;

/// <summary>
/// The users table exercised against a real PostgreSQL.
/// </summary>
/// <remarks>
/// These assertions cannot be made against a fake. The unique index, the case-insensitive
/// collision it rejects, and the enum stored as text are all properties of the database, and
/// <c>.claude/rules/backend.md</c> bans the in-memory provider for exactly this reason.
/// </remarks>
[Collection(IntegrationDatabase.Name)]
[Trait("Category", "Integration")]
public class UserPersistenceTests(PostgresFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset _now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AUserSurvivesARoundTripThroughTheDatabase()
    {
        var user = User.Register("Mattia", "a-hash", _now);

        await using (var scope = NewScope())
        {
            scope.Context.Set<User>().Add(user);
            await scope.Context.SaveChangesAsync();
        }

        await using var reading = NewScope();
        var stored = await reading.Context.Set<User>().SingleAsync();

        Assert.Equal(user.Id, stored.Id);
        Assert.Equal("Mattia", stored.Username);
        Assert.Equal("mattia", stored.UsernameNormalized);
        Assert.Equal("a-hash", stored.PasswordHash);
        Assert.Equal(AccountState.Pending, stored.State);
        Assert.Equal(_now, stored.CreatedAt);
    }

    [Fact]
    public async Task TheAccountStateIsStoredAsReadableText()
    {
        await using (var scope = NewScope())
        {
            scope.Context.Set<User>().Add(User.Register("Mattia", "a-hash", _now));
            await scope.Context.SaveChangesAsync();
        }

        // Read with raw SQL rather than through EF, because EF would convert the value back and
        // hide exactly what this test exists to check.
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT state FROM users", connection);

        Assert.Equal("Pending", await command.ExecuteScalarAsync() as string);
    }

    [Fact]
    public async Task TwoUsernamesDifferingOnlyInCaseCannotBothExist()
    {
        await using (var scope = NewScope())
        {
            scope.Context.Set<User>().Add(User.Register("Mattia", "a-hash", _now));
            await scope.Context.SaveChangesAsync();
        }

        await using var second = NewScope();
        second.Context.Set<User>().Add(User.Register("mattia", "another-hash", _now));

        // The database rejects it, not application code. A check-then-insert in a service would
        // leave a window between the two statements for a concurrent registration to take the same
        // name; a unique index has no such window.
        var failure = await Assert.ThrowsAsync<DbUpdateException>(
            () => second.Context.SaveChangesAsync());

        Assert.IsType<PostgresException>(failure.InnerException);
        Assert.Equal(
            PostgresErrorCodes.UniqueViolation,
            ((PostgresException)failure.InnerException!).SqlState);
    }

    [Fact]
    public async Task TheSameUsernameInTheSameCaseCannotBeRegisteredTwice()
    {
        await using (var scope = NewScope())
        {
            scope.Context.Set<User>().Add(User.Register("Mattia", "a-hash", _now));
            await scope.Context.SaveChangesAsync();
        }

        await using var second = NewScope();
        second.Context.Set<User>().Add(User.Register("Mattia", "another-hash", _now));

        await Assert.ThrowsAsync<DbUpdateException>(() => second.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task EachTestStartsWithAnEmptyTable()
    {
        // The one that proves Respawn is doing its job: every other test in this class inserts
        // "Mattia", and this one would fail the moment resetting stopped working.
        await using var scope = NewScope();

        Assert.Empty(await scope.Context.Set<User>().ToListAsync());
    }

    private Scope NewScope() => new(fixture.Factory.Services.CreateScope());

    /// <remarks>
    /// A fresh scope per unit of work, because a <c>DbContext</c> caches what it has already
    /// tracked — reading back through the same instance would return the object still in memory
    /// and prove nothing about what reached PostgreSQL.
    /// </remarks>
    private sealed class Scope(IServiceScope scope) : IAsyncDisposable
    {
        public VttDbContext Context { get; } = scope.ServiceProvider.GetRequiredService<VttDbContext>();

        public ValueTask DisposeAsync()
        {
            scope.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
