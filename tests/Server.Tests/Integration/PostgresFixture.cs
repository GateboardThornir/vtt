using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

    public WebApplicationFactory<Program> Factory =>
        _factory ?? throw new InvalidOperationException("The fixture has not been initialised.");

    public HttpClient CreateClient() => Factory.CreateClient();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

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
    }

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
