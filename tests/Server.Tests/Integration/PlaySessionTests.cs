using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Vtt.Server.Accounts;
using Vtt.Server.Campaigns;
using Vtt.Server.Infrastructure;
using Vtt.Server.Sessions;

namespace Vtt.Server.Tests.Integration;

[Collection(IntegrationDatabase.Name)]
[Trait("Category", "Integration")]
public class PlaySessionTests(PostgresFixture fixture) : IAsyncLifetime
{
    private const string Password = "a perfectly ordinary passphrase";

    private static readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private Guid _campaign;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await CreateAccountAsync("Master");
        await CreateAccountAsync("Player");
        await CreateAccountAsync("Stranger");

        using var master = await SignedInAsync("Master");
        _campaign = await CreateCampaignAsync(master);

        await master.PostAsJsonAsync($"/api/campaigns/{_campaign}/roster", new InviteMemberRequest("Player"));

        using var player = await SignedInAsync("Player");
        await player.PostAsJsonAsync(
            $"/api/campaigns/{_campaign}/roster/response",
            new RespondToInvitationRequest(true));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ASessionStartsPlannedAndCanBeOpenedThenClosed()
    {
        using var master = await SignedInAsync("Master");
        var session = await CreateSessionAsync(master, "Session one");

        Assert.Equal(SessionState.Planned, session.State);

        Assert.Equal(HttpStatusCode.NoContent, (await SetStateAsync(master, session.Id, SessionState.Open)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SetStateAsync(master, session.Id, SessionState.Closed)).StatusCode);

        var listed = await ListAsync(master);
        Assert.Equal(SessionState.Closed, Assert.Single(listed).State);
    }

    [Fact]
    public async Task OnlyOneSessionCanBeOpenAtATime()
    {
        using var master = await SignedInAsync("Master");
        var first = await CreateSessionAsync(master, "First");
        var second = await CreateSessionAsync(master, "Second");

        Assert.Equal(HttpStatusCode.NoContent, (await SetStateAsync(master, first.Id, SessionState.Open)).StatusCode);

        // The partial unique index is what decides, not a check beforehand — the window between
        // reading and writing is where concurrency lives.
        Assert.Equal(HttpStatusCode.Conflict, (await SetStateAsync(master, second.Id, SessionState.Open)).StatusCode);
    }

    [Fact]
    public async Task AnotherSessionCanOpenOnceTheFirstIsClosed()
    {
        using var master = await SignedInAsync("Master");
        var first = await CreateSessionAsync(master, "First");
        var second = await CreateSessionAsync(master, "Second");

        await SetStateAsync(master, first.Id, SessionState.Open);
        await SetStateAsync(master, first.Id, SessionState.Closed);

        Assert.Equal(HttpStatusCode.NoContent, (await SetStateAsync(master, second.Id, SessionState.Open)).StatusCode);
    }

    [Fact]
    public async Task ParallelAttemptsToOpenTwoSessionsProduceExactlyOneWinner()
    {
        using var master = await SignedInAsync("Master");
        var sessions = new List<Guid>();

        for (var index = 0; index < 6; index++)
        {
            sessions.Add((await CreateSessionAsync(master, $"Session {index}")).Id);
        }

        var responses = await Task.WhenAll(
            sessions.Select(id => Task.Run(async () =>
            {
                using var client = await SignedInAsync("Master");
                return await SetStateAsync(client, id, SessionState.Open);
            })));

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.NoContent));
    }

    [Fact]
    public async Task AClosedSessionCannotBeReopened()
    {
        using var master = await SignedInAsync("Master");
        var session = await CreateSessionAsync(master, "Session one");

        await SetStateAsync(master, session.Id, SessionState.Closed);

        Assert.Equal(HttpStatusCode.Conflict, (await SetStateAsync(master, session.Id, SessionState.Open)).StatusCode);
    }

    [Fact]
    public async Task APlayerCanSeeSessionsButNotChangeThem()
    {
        using var master = await SignedInAsync("Master");
        var session = await CreateSessionAsync(master, "Session one");

        using var player = await SignedInAsync("Player");

        Assert.Single(await ListAsync(player));
        Assert.Equal(HttpStatusCode.Forbidden, (await SetStateAsync(player, session.Id, SessionState.Open)).StatusCode);

        var create = await player.PostAsJsonAsync(
            $"/api/campaigns/{_campaign}/sessions",
            new CreateSessionRequest("Mine"));

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task AStrangerSeesNothingAtAll()
    {
        using var master = await SignedInAsync("Master");
        var session = await CreateSessionAsync(master, "Session one");

        using var stranger = await SignedInAsync("Stranger");

        // 404 everywhere, matching the campaign: a stranger is not entitled to know it exists.
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await stranger.GetAsync(new Uri($"/api/campaigns/{_campaign}/sessions", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SetStateAsync(stranger, session.Id, SessionState.Open)).StatusCode);
    }

    [Fact]
    public async Task ClosingASessionKeepsItInTheHistory()
    {
        using var master = await SignedInAsync("Master");
        var session = await CreateSessionAsync(master, "Session one");

        await SetStateAsync(master, session.Id, SessionState.Open);
        await SetStateAsync(master, session.Id, SessionState.Closed);

        // Phase 2's event log will hang off these rows; closing must never delete anything.
        var listed = Assert.Single(await ListAsync(master));
        Assert.NotNull(listed.OpenedAt);
        Assert.NotNull(listed.ClosedAt);
    }

    [Fact]
    public async Task AnEmptyTitleIsRefused()
    {
        using var master = await SignedInAsync("Master");

        var response = await master.PostAsJsonAsync(
            $"/api/campaigns/{_campaign}/sessions",
            new CreateSessionRequest("   "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<PlaySessionView> CreateSessionAsync(HttpClient client, string title)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/campaigns/{_campaign}/sessions",
            new CreateSessionRequest(title));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<PlaySessionView>(_jsonOptions))!;
    }

    private Task<HttpResponseMessage> SetStateAsync(HttpClient client, Guid sessionId, SessionState state) =>
        client.PutAsJsonAsync(
            $"/api/campaigns/{_campaign}/sessions/{sessionId}/state",
            new SetSessionStateRequest(state));

    private async Task<List<PlaySessionView>> ListAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<PlaySessionView>>(
            $"/api/campaigns/{_campaign}/sessions",
            _jsonOptions))!;

    private static async Task<Guid> CreateCampaignAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/campaigns",
            new CreateCampaignRequest("A campaign", "dnd5e", "1.0.0"));

        return (await response.Content.ReadFromJsonAsync<CampaignSummary>(_jsonOptions))!.Id;
    }

    private async Task<HttpClient> SignedInAsync(string username)
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/session", new SignInRequest(username, Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return client;
    }

    private async Task CreateAccountAsync(string username)
    {
        await using var scope = new Scope(fixture.Factory.Services.CreateScope());
        var hash = scope.Provider.GetRequiredService<IPasswordHasher>().Hash(Password);
        var user = User.CreateActive(username, hash, fixture.Clock.GetUtcNow());

        scope.Context.Entry(user).Property(nameof(User.Role)).CurrentValue = PlatformRole.Member;
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
