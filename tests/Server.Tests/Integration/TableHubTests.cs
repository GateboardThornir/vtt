using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Vtt.Server.Accounts;
using Vtt.Server.Campaigns;
using Vtt.Server.Infrastructure;
using Vtt.Server.Sessions;
using Vtt.Server.Table;

namespace Vtt.Server.Tests.Integration;

/// <summary>
/// The hub over a real connection, not a mocked one.
/// </summary>
/// <remarks>
/// A hub method is a public endpoint with no visible URL, which is exactly why these tests drive
/// the transport rather than calling the class: the authorisation has to hold where a client can
/// actually reach it.
/// </remarks>
[Collection(IntegrationDatabase.Name)]
[Trait("Category", "Integration")]
public class TableHubTests(PostgresFixture fixture) : IAsyncLifetime
{
    private const string Password = "a perfectly ordinary passphrase";

    private static readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private Guid _campaign;
    private Guid _openSession;
    private Guid _plannedSession;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await CreateAccountAsync("Master");
        await CreateAccountAsync("Player");
        await CreateAccountAsync("Stranger");

        using var master = await SignedInAsync("Master");

        var campaign = await master.PostAsJsonAsync(
            "/api/campaigns",
            new CreateCampaignRequest("A campaign", "dnd5e", "1.0.0"));

        _campaign = (await campaign.Content.ReadFromJsonAsync<CampaignSummary>(_jsonOptions))!.Id;

        await master.PostAsJsonAsync($"/api/campaigns/{_campaign}/roster", new InviteMemberRequest("Player"));

        using var player = await SignedInAsync("Player");
        await player.PostAsJsonAsync(
            $"/api/campaigns/{_campaign}/roster/response",
            new RespondToInvitationRequest(true));

        _openSession = await CreateSessionAsync(master, "Live");
        _plannedSession = await CreateSessionAsync(master, "Later");

        await master.PutAsJsonAsync(
            $"/api/campaigns/{_campaign}/sessions/{_openSession}/state",
            new SetSessionStateRequest(SessionState.Open));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AMemberCanJoinAnOpenSession()
    {
        await using var connection = await ConnectAsync("Master");

        Assert.True(await connection.InvokeAsync<bool>("JoinSession", _openSession));
    }

    [Fact]
    public async Task AStrangerCannotJoin()
    {
        await using var connection = await ConnectAsync("Stranger");

        // False and nothing else: the refusal says nothing about whether the session exists.
        Assert.False(await connection.InvokeAsync<bool>("JoinSession", _openSession));
    }

    [Fact]
    public async Task ASessionThatIsNotOpenCannotBeJoined()
    {
        await using var connection = await ConnectAsync("Master");

        Assert.False(await connection.InvokeAsync<bool>("JoinSession", _plannedSession));
    }

    [Fact]
    public async Task AnUnknownSessionCannotBeJoined()
    {
        await using var connection = await ConnectAsync("Master");

        Assert.False(await connection.InvokeAsync<bool>("JoinSession", Guid.NewGuid()));
    }

    [Fact]
    public async Task AnAnonymousConnectionIsRefused()
    {
        await using var connection = Build(fixture.CreateClient());

        // The hub carries the same policy as the HTTP API, so an unauthenticated handshake never
        // reaches a hub method at all.
        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }

    [Fact]
    public async Task JoiningTellsTheNewcomerWhoIsAlreadyThere()
    {
        await using var master = await ConnectAsync("Master");
        await master.InvokeAsync<bool>("JoinSession", _openSession);

        var seen = new TaskCompletionSource<IReadOnlyList<Participant>>();

        await using var player = await ConnectAsync("Player");
        player.On<IReadOnlyList<Participant>>("Participants", list => seen.TrySetResult(list));

        await player.InvokeAsync<bool>("JoinSession", _openSession);

        var participants = await seen.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal("Master", Assert.Single(participants).Username);
    }

    [Fact]
    public async Task ParticipantsSeeEachOtherArrive()
    {
        var arrived = new TaskCompletionSource<Participant>();

        await using var master = await ConnectAsync("Master");
        master.On<Participant>("ParticipantJoined", participant => arrived.TrySetResult(participant));
        await master.InvokeAsync<bool>("JoinSession", _openSession);

        await using var player = await ConnectAsync("Player");
        await player.InvokeAsync<bool>("JoinSession", _openSession);

        Assert.Equal("Player", (await arrived.Task.WaitAsync(TimeSpan.FromSeconds(10))).Username);
    }

    [Fact]
    public async Task LeavingIsAnnounced()
    {
        var left = new TaskCompletionSource<Participant>();

        await using var master = await ConnectAsync("Master");
        master.On<Participant>("ParticipantLeft", participant => left.TrySetResult(participant));
        await master.InvokeAsync<bool>("JoinSession", _openSession);

        var player = await ConnectAsync("Player");
        await player.InvokeAsync<bool>("JoinSession", _openSession);
        await player.InvokeAsync("LeaveSession", _openSession);

        Assert.Equal("Player", (await left.Task.WaitAsync(TimeSpan.FromSeconds(10))).Username);

        await player.DisposeAsync();
    }

    [Fact]
    public async Task DisconnectingRemovesTheParticipant()
    {
        var left = new TaskCompletionSource<Participant>();

        await using var master = await ConnectAsync("Master");
        master.On<Participant>("ParticipantLeft", participant => left.TrySetResult(participant));
        await master.InvokeAsync<bool>("JoinSession", _openSession);

        var player = await ConnectAsync("Player");
        await player.InvokeAsync<bool>("JoinSession", _openSession);
        await player.DisposeAsync();

        Assert.Equal("Player", (await left.Task.WaitAsync(TimeSpan.FromSeconds(10))).Username);
    }

    [Fact]
    public async Task ASecondTabDoesNotAnnounceASecondArrival()
    {
        // Counted per account, not per connection: closing one tab must not tell the table that
        // somebody left while they are still sitting at it.
        await using var master = await ConnectAsync("Master");
        await master.InvokeAsync<bool>("JoinSession", _openSession);

        var announcements = 0;
        master.On<Participant>("ParticipantJoined", _ => announcements++);

        await using var firstTab = await ConnectAsync("Player");
        await firstTab.InvokeAsync<bool>("JoinSession", _openSession);

        await using var secondTab = await ConnectAsync("Player");
        await secondTab.InvokeAsync<bool>("JoinSession", _openSession);

        await Task.Delay(300);

        Assert.Equal(1, announcements);
    }

    private async Task<HubConnection> ConnectAsync(string username)
    {
        var client = await SignedInAsync(username);
        var connection = Build(client);

        await connection.StartAsync();

        return connection;
    }

    private HubConnection Build(HttpClient client) =>
        new HubConnectionBuilder()
            .WithUrl(
                new Uri(fixture.Factory.Server.BaseAddress, "/hubs/table"),
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => fixture.Factory.Server.CreateHandler();
                    options.Headers["Cookie"] = CookieOf(client);

                    // Long polling, not WebSockets. The in-memory test server does not carry a raw
                    // WebSocket through an ordinary message handler, and the transport is not what
                    // these tests are about — the authorisation, the groups and the lifecycle are.
                    options.Transports = HttpTransportType.LongPolling;
                    options.SkipNegotiation = false;
                })
            .Build();

    private static string CookieOf(HttpClient client) =>
        client.DefaultRequestHeaders.TryGetValues("Cookie", out var values)
            ? string.Join("; ", values)
            : string.Empty;

    private async Task<Guid> CreateSessionAsync(HttpClient client, string title)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/campaigns/{_campaign}/sessions",
            new CreateSessionRequest(title));

        return (await response.Content.ReadFromJsonAsync<PlaySessionView>(_jsonOptions))!.Id;
    }

    private async Task<HttpClient> SignedInAsync(string username)
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/session", new SignInRequest(username, Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The handler does not keep cookies, so the one just issued is carried by hand onto both
        // later requests and the hub handshake.
        client.DefaultRequestHeaders.Add(
            "Cookie",
            response.Headers.GetValues("Set-Cookie").Select(value => value.Split(';')[0]));

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
