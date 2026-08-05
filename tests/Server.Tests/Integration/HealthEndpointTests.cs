using System.Net;
using System.Text.Json;

namespace Vtt.Server.Tests.Integration;

/// <summary>
/// The health endpoint exercised through the real HTTP pipeline against a real PostgreSQL.
/// </summary>
/// <remarks>
/// This is the shape every later integration test takes: the application is the one
/// <c>Program.cs</c> builds — real dependency injection, real middleware, real EF Core — and only
/// the socket is replaced. Instantiating the health check directly would prove the check works and
/// nothing about whether it is wired up, which is the part that actually breaks.
/// <para>
/// There is deliberately no test of the unhealthy path. With the database unreachable that request
/// takes around sixteen seconds, which is Npgsql's default connection timeout; the fix is recorded
/// in <c>PROGRESS.md</c> against task 101, and a test would simply inherit the latency.
/// </para>
/// </remarks>
[Collection(IntegrationDatabase.Name)]
[Trait("Category", "Integration")]
public class HealthEndpointTests(PostgresFixture fixture)
{
    [Fact]
    public async Task HealthEndpointReturnsOkWhenTheDatabaseIsReachable()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpointReportsTheDatabaseCheckAsHealthy()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/health", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        using var report = JsonDocument.Parse(body);
        Assert.Equal("Healthy", report.RootElement.GetProperty("status").GetString());
        Assert.Equal(
            "Healthy",
            report.RootElement.GetProperty("checks").GetProperty("database").GetString());
    }

    [Fact]
    public async Task HealthEndpointDoesNotLeakConnectionDetails()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/health", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        // /api/health is unauthenticated. Npgsql failure messages carry the host, port and
        // username, so the response writer emits statuses only — see .claude/rules/security.md.
        Assert.DoesNotContain("Host=", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Username=", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);
    }
}
