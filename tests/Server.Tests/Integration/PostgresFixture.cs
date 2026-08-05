using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using Vtt.Server.Infrastructure;

namespace Vtt.Server.Tests.Integration;

/// <summary>
/// Owns a throwaway PostgreSQL container and an in-process instance of the real application.
/// </summary>
/// <remarks>
/// The container is deliberately not the one <c>docker-compose.yml</c> runs: these tests migrate
/// and write freely, and a suite whose first casualty is the maintainer's own campaign would stop
/// being run. It is created once for the whole collection — container startup dominates the run,
/// and a container per test class is how a suite becomes too slow to bother with.
/// </remarks>
[SuppressMessage(
    "Microsoft.Design",
    "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable",
    Justification = "Disposed through xUnit's IAsyncLifetime, which the analyser does not model.")]
public sealed class PostgresFixture : IAsyncLifetime
{
    // Same major as docker-compose.yml. Tests passing against a different major than development
    // and production run would be a comfortable lie.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18").Build();

    private string? _previousConnectionString;
    private WebApplicationFactory<Program>? _factory;
    private Respawner? _respawner;

    public WebApplicationFactory<Program> Factory =>
        _factory ?? throw new InvalidOperationException("The fixture has not been initialised.");

    public HttpClient CreateClient() => Factory.CreateClient();

    public async Task InitializeAsync()
    {
        try
        {
            await _container.StartAsync();
        }
        catch (Exception exception)
        {
            // Without this, a stopped Docker surfaces as whatever low-level socket or HTTP error
            // the client happened to produce, which reads like a fault in the code under test.
            throw new InvalidOperationException(
                "Could not start the PostgreSQL test container. These tests need Docker running " +
                "(see ADR 005). To run everything that does not, use: " +
                "dotnet test --filter \"Category!=Integration\".",
                exception);
        }

        // An environment variable rather than WebApplicationFactory's UseSetting or
        // ConfigureAppConfiguration, and the reason is ordering. Program.cs resolves the
        // connection string *before* builder.Build(), while those hooks are applied when the
        // factory intercepts the build — too late for code that has already run. Environment
        // variables are read by WebApplication.CreateBuilder's default providers, so the value is
        // in place before the fail-fast check executes.
        _previousConnectionString = Environment.GetEnvironmentVariable(EnvironmentVariable);
        Environment.SetEnvironmentVariable(EnvironmentVariable, _container.GetConnectionString());

        _factory = new WebApplicationFactory<Program>();

        // Through the application's own service provider, so this also proves AddVttDatabase
        // registered the context correctly. Applying migrations here does not contradict ADR 003:
        // that decision forbids the *application* migrating at startup, and a fixture building a
        // database it owns is exactly the deliberate, explicit application the ADR asks for.
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<VttDbContext>().Database.MigrateAsync();

        // Built after migrating, because Respawn inspects the schema to work out the order in
        // which tables can be emptied without tripping foreign keys.
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(
            connection,
            new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = ["public"],

                // Emptying this would tell EF the schema had never been built, and the next
                // migration run would try to create tables that already exist.
                TablesToIgnore = [new Respawn.Graph.Table("public", "__EFMigrationsHistory")],
            });
    }

    /// <summary>
    /// Empties every table, leaving the schema and the migration history intact.
    /// </summary>
    /// <remarks>
    /// Called before each integration test. Until task 010 the integration tests wrote nothing, so
    /// the container's contents did not matter; the moment tests insert rows, one test's leftovers
    /// become the next one's inexplicable failure — and the failure usually depends on execution
    /// order, which makes it look like flakiness.
    /// </remarks>
    public async Task ResetAsync()
    {
        if (_respawner is null)
        {
            throw new InvalidOperationException("The fixture has not been initialised.");
        }

        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();

        await _respawner.ResetAsync(connection);
    }

    /// <summary>Opens a connection to the test database, for assertions that need raw SQL.</summary>
    public NpgsqlConnection CreateConnection() => new(_container.GetConnectionString());

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        Environment.SetEnvironmentVariable(EnvironmentVariable, _previousConnectionString);

        await _container.DisposeAsync();
    }

    private const string EnvironmentVariable = "ConnectionStrings__Default";
}
