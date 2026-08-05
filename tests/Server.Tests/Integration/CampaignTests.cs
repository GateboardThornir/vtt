using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Vtt.Server.Accounts;
using Vtt.Server.Campaigns;
using Vtt.Server.Infrastructure;

namespace Vtt.Server.Tests.Integration;

[Collection(IntegrationDatabase.Name)]
[Trait("Category", "Integration")]
public class CampaignTests(PostgresFixture fixture) : IAsyncLifetime
{
    private const string Password = "a perfectly ordinary passphrase";

    /// <remarks>Mirrors the server: enums cross the wire as names, so a reader needs the converter.</remarks>
    private static readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreatingACampaignMakesYouItsMaster()
    {
        await CreateAccountAsync("Mattia");
        using var client = await SignedInAsync("Mattia");

        var response = await CreateCampaignAsync(client, "Rime of the Frostmaiden");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var listed = await client.GetFromJsonAsync<List<CampaignSummary>>("/api/campaigns", _jsonOptions);
        Assert.Equal("Rime of the Frostmaiden", Assert.Single(listed!).Name);
    }

    [Fact]
    public async Task ThePinnedSystemIsStoredExactlyAsSupplied()
    {
        // Recorded, not validated: no registry exists until 030. Storing it from the first campaign
        // is what guarantees no campaign ever exists without a pin.
        await CreateAccountAsync("Mattia");
        using var client = await SignedInAsync("Mattia");

        var response = await client.PostAsJsonAsync(
            "/api/campaigns",
            new CreateCampaignRequest("A campaign", "dnd5e", "1.0.0"));

        var created = await response.Content.ReadFromJsonAsync<CampaignSummary>(_jsonOptions);

        Assert.Equal("dnd5e", created?.SystemId);
        Assert.Equal("1.0.0", created?.SystemVersion);
    }

    [Fact]
    public async Task AnUnregisteredSystemIsRefused()
    {
        // Task 020 accepted anything, deliberately: a hardcoded list would have been a second
        // source of truth that 030's registry then had to remove. The registry is that source of
        // truth, so the check now exists and this is the test that used to assert the opposite.
        await CreateAccountAsync("Mattia");
        using var client = await SignedInAsync("Mattia");

        var response = await client.PostAsJsonAsync(
            "/api/campaigns",
            new CreateCampaignRequest("A campaign", "some-future-system", "0.1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("system_unknown", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AKnownSystemAtAnUnknownVersionIsRefused()
    {
        // The pin is both halves. A module existing does not make every version of it exist.
        await CreateAccountAsync("Mattia");
        using var client = await SignedInAsync("Mattia");

        var response = await client.PostAsJsonAsync(
            "/api/campaigns",
            new CreateCampaignRequest("A campaign", "dnd5e", "99.0.0"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SomebodyElsesCampaignDoesNotAppearInYourList()
    {
        await CreateAccountAsync("Mattia");
        await CreateAccountAsync("Stranger");

        using var mine = await SignedInAsync("Mattia");
        await CreateCampaignAsync(mine, "Mine");

        using var theirs = await SignedInAsync("Stranger");
        var listed = await theirs.GetFromJsonAsync<List<CampaignSummary>>("/api/campaigns", _jsonOptions);

        Assert.Empty(listed!);
    }

    [Fact]
    public async Task FetchingSomebodyElsesCampaignIsANotFoundRatherThanAForbidden()
    {
        await CreateAccountAsync("Mattia");
        await CreateAccountAsync("Stranger");

        using var mine = await SignedInAsync("Mattia");
        var created = await (await CreateCampaignAsync(mine, "Mine")).Content.ReadFromJsonAsync<CampaignSummary>(_jsonOptions);

        using var theirs = await SignedInAsync("Stranger");
        var response = await theirs.GetAsync(new Uri($"/api/campaigns/{created!.Id}", UriKind.Relative));

        // 403 would confirm the campaign exists. Which campaigns a private group runs is not
        // public information, so a stranger gets the same answer as for an id that never existed.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var nonexistent = await theirs.GetAsync(new Uri($"/api/campaigns/{Guid.NewGuid()}", UriKind.Relative));
        Assert.Equal(nonexistent.StatusCode, response.StatusCode);
        Assert.Equal(
            await nonexistent.Content.ReadAsStringAsync(),
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task APlatformAdministratorSeesNoCampaignsButTheirOwn()
    {
        // Being a platform administrator grants no campaign access whatsoever — the rule in
        // .claude/rules/security.md that the whole visibility model depends on.
        await CreateAccountAsync("Mattia");
        await CreateAccountAsync("Boss", admin: true);

        using var mattia = await SignedInAsync("Mattia");
        await CreateCampaignAsync(mattia, "Not the administrator's business");

        using var boss = await SignedInAsync("Boss");
        Assert.Empty((await boss.GetFromJsonAsync<List<CampaignSummary>>("/api/campaigns", _jsonOptions))!);
    }

    [Fact]
    public async Task AnAnonymousCallerIsRefused()
    {
        using var client = fixture.CreateClient();

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync(new Uri("/api/campaigns", UriKind.Relative))).StatusCode);
    }

    [Theory]
    [InlineData("", "dnd5e", "1.0.0", "name_invalid")]
    [InlineData("   ", "dnd5e", "1.0.0", "name_invalid")]
    [InlineData("Fine", "", "1.0", "system_invalid")]
    [InlineData("Fine", "dnd5e", "", "system_invalid")]
    public async Task MalformedInputIsRefusedAtTheBoundary(
        string name,
        string systemId,
        string version,
        string expected)
    {
        await CreateAccountAsync("Mattia");
        using var client = await SignedInAsync("Mattia");

        var response = await client.PostAsJsonAsync(
            "/api/campaigns",
            new CreateCampaignRequest(name, systemId, version));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expected, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ANameIsTrimmedBeforeItIsStored()
    {
        await CreateAccountAsync("Mattia");
        using var client = await SignedInAsync("Mattia");

        var created = await (await CreateCampaignAsync(client, "  Padded  "))
            .Content.ReadFromJsonAsync<CampaignSummary>(_jsonOptions);

        Assert.Equal("Padded", created?.Name);
    }

    private static Task<HttpResponseMessage> CreateCampaignAsync(HttpClient client, string name) =>
        client.PostAsJsonAsync("/api/campaigns", new CreateCampaignRequest(name, "dnd5e", "1.0.0"));

    private async Task<HttpClient> SignedInAsync(string username)
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/session", new SignInRequest(username, Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return client;
    }

    private async Task CreateAccountAsync(string username, bool admin = false)
    {
        await using var scope = new Scope(fixture.Factory.Services.CreateScope());
        var hash = scope.Provider.GetRequiredService<IPasswordHasher>().Hash(Password);
        var user = User.CreateActive(username, hash, fixture.Clock.GetUtcNow());

        if (!admin)
        {
            scope.Context.Entry(user).Property(nameof(User.Role)).CurrentValue = PlatformRole.Member;
        }

        scope.Context.Set<User>().Add(user);
        await scope.Context.SaveChangesAsync();
    }

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
