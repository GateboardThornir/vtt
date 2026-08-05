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

    [Fact]
    public async Task AMemberCanSpeakAndTheTableHearsIt()
    {
        var heard = new TaskCompletionSource<ChatLine>();

        await using var master = await ConnectAsync("Master");
        master.On<ChatLine>("ChatSaid", line => heard.TrySetResult(line));
        await master.InvokeAsync<bool>("JoinSession", _openSession);

        await using var player = await ConnectAsync("Player");
        await player.InvokeAsync<bool>("JoinSession", _openSession);

        Assert.True(await player.InvokeAsync<bool>("Say", _openSession, "Well met.", ChatVoice.InCharacter));

        var line = await heard.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal("Well met.", line.Body);
        Assert.Equal("Player", line.AuthorUsername);
        Assert.Equal(ChatVoice.InCharacter, line.Voice);
    }

    [Fact]
    public async Task HistoryArrivesOnJoinSoAMessageSurvivesAReconnect()
    {
        await using var first = await ConnectAsync("Master");
        await first.InvokeAsync<bool>("JoinSession", _openSession);
        await first.InvokeAsync<bool>("Say", _openSession, "Before the crash.", ChatVoice.OutOfCharacter);

        var history = new TaskCompletionSource<IReadOnlyList<ChatLine>>();

        await using var second = await ConnectAsync("Player");
        second.On<IReadOnlyList<ChatLine>>("ChatHistory", lines => history.TrySetResult(lines));
        await second.InvokeAsync<bool>("JoinSession", _openSession);

        var lines = await history.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal("Before the crash.", Assert.Single(lines).Body);
    }

    [Fact]
    public async Task AStrangerCannotSpeakAtATableTheyWereNeverAdmittedTo()
    {
        await using var stranger = await ConnectAsync("Stranger");

        Assert.False(await stranger.InvokeAsync<bool>("Say", _openSession, "Hello?", ChatVoice.OutOfCharacter));
    }

    [Fact]
    public async Task SomebodyRemovedFromTheRosterCanNoLongerSpeak()
    {
        // Admission is re-checked on every send. Being in the group is not proof of anything later:
        // the connection stays in it until the client notices, and the check is what stops them.
        await using var player = await ConnectAsync("Player");
        await player.InvokeAsync<bool>("JoinSession", _openSession);
        Assert.True(await player.InvokeAsync<bool>("Say", _openSession, "Still here.", ChatVoice.OutOfCharacter));

        using var master = await SignedInAsync("Master");
        var roster = await master.GetFromJsonAsync<List<RosterEntry>>(
            $"/api/campaigns/{_campaign}/roster",
            _jsonOptions);
        var playerId = roster!.Single(entry => entry.Username == "Player").UserId;

        await master.DeleteAsync(new Uri($"/api/campaigns/{_campaign}/roster/{playerId}", UriKind.Relative));

        Assert.False(await player.InvokeAsync<bool>("Say", _openSession, "Let me back in.", ChatVoice.OutOfCharacter));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnEmptyMessageIsRefused(string body)
    {
        await using var master = await ConnectAsync("Master");
        await master.InvokeAsync<bool>("JoinSession", _openSession);

        Assert.False(await master.InvokeAsync<bool>("Say", _openSession, body, ChatVoice.OutOfCharacter));
    }

    [Fact]
    public async Task AnOversizedMessageIsRefused()
    {
        await using var master = await ConnectAsync("Master");
        await master.InvokeAsync<bool>("JoinSession", _openSession);

        var tooLong = new string('a', ChatMessage.BodyMaxLength + 1);

        Assert.False(await master.InvokeAsync<bool>("Say", _openSession, tooLong, ChatVoice.OutOfCharacter));
    }

    [Fact]
    public async Task BothVoicesRoundTrip()
    {
        await using var master = await ConnectAsync("Master");
        await master.InvokeAsync<bool>("JoinSession", _openSession);

        await master.InvokeAsync<bool>("Say", _openSession, "In character.", ChatVoice.InCharacter);
        await master.InvokeAsync<bool>("Say", _openSession, "Out of character.", ChatVoice.OutOfCharacter);

        var history = new TaskCompletionSource<IReadOnlyList<ChatLine>>();
        await using var listener = await ConnectAsync("Player");
        listener.On<IReadOnlyList<ChatLine>>("ChatHistory", lines => history.TrySetResult(lines));
        await listener.InvokeAsync<bool>("JoinSession", _openSession);

        var lines = await history.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(ChatVoice.InCharacter, lines[0].Voice);
        Assert.Equal(ChatVoice.OutOfCharacter, lines[1].Voice);
    }

    [Fact]
    public async Task APublicRollReachesEveryoneAtTheTable()
    {
        var heard = new TaskCompletionSource<RollLine>();

        await using var master = await ConnectAsync("Master");
        master.On<RollLine>("Rolled", roll => heard.TrySetResult(roll));
        await master.InvokeAsync<bool>("JoinSession", _openSession);

        await using var player = await ConnectAsync("Player");
        await player.InvokeAsync<bool>("JoinSession", _openSession);

        Assert.True(await player.InvokeAsync<bool>("Roll", _openSession, "2d6+3", RollVisibility.Public));

        var roll = await heard.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal("2d6+3", roll.Expression);
        Assert.Equal(2, roll.Kept.Count);
        Assert.Equal(roll.Kept.Sum() + roll.Modifier, roll.Total);
    }

    [Fact]
    public async Task APrivateRollReachesTheRollerAndTheMasterAndNobodyElse()
    {
        await CreateAccountAsync("Second");

        using var master = await SignedInAsync("Master");
        await master.PostAsJsonAsync($"/api/campaigns/{_campaign}/roster", new InviteMemberRequest("Second"));

        using var secondHttp = await SignedInAsync("Second");
        await secondHttp.PostAsJsonAsync(
            $"/api/campaigns/{_campaign}/roster/response",
            new RespondToInvitationRequest(true));

        var masterHeard = new TaskCompletionSource<RollLine>();
        var rollerHeard = new TaskCompletionSource<RollLine>();
        var bystanderPayloads = new List<RollLine>();

        await using var masterConnection = await ConnectAsync("Master");
        masterConnection.On<RollLine>("Rolled", roll => masterHeard.TrySetResult(roll));
        await masterConnection.InvokeAsync<bool>("JoinSession", _openSession);

        await using var bystander = await ConnectAsync("Second");
        bystander.On<RollLine>("Rolled", roll => bystanderPayloads.Add(roll));
        await bystander.InvokeAsync<bool>("JoinSession", _openSession);

        await using var roller = await ConnectAsync("Player");
        roller.On<RollLine>("Rolled", roll => rollerHeard.TrySetResult(roll));
        await roller.InvokeAsync<bool>("JoinSession", _openSession);

        Assert.True(await roller.InvokeAsync<bool>("Roll", _openSession, "d20", RollVisibility.Private));

        var seenByMaster = await masterHeard.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var seenByRoller = await rollerHeard.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(seenByRoller.Id, seenByMaster.Id);

        await Task.Delay(500);

        // The half that matters: the bystander received *nothing at all*. Not a redacted event and
        // not a placeholder — "somebody rolled something" is itself a disclosure.
        Assert.Empty(bystanderPayloads);
    }

    [Fact]
    public async Task AMasterOnlyRollReachesTheMasterAlone()
    {
        var masterHeard = new TaskCompletionSource<RollLine>();
        var playerPayloads = new List<RollLine>();

        await using var masterConnection = await ConnectAsync("Master");
        masterConnection.On<RollLine>("Rolled", roll => masterHeard.TrySetResult(roll));
        await masterConnection.InvokeAsync<bool>("JoinSession", _openSession);

        await using var player = await ConnectAsync("Player");
        player.On<RollLine>("Rolled", roll => playerPayloads.Add(roll));
        await player.InvokeAsync<bool>("JoinSession", _openSession);

        Assert.True(await masterConnection.InvokeAsync<bool>("Roll", _openSession, "d20", RollVisibility.MasterOnly));

        await masterHeard.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(500);

        Assert.Empty(playerPayloads);
    }

    [Fact]
    public async Task APlayerCannotMakeAMasterOnlyRoll()
    {
        await using var player = await ConnectAsync("Player");
        await player.InvokeAsync<bool>("JoinSession", _openSession);

        // Hiding a roll from the person running the game is not a thing the table means.
        Assert.False(await player.InvokeAsync<bool>("Roll", _openSession, "d20", RollVisibility.MasterOnly));
    }

    [Fact]
    public async Task HistoryHidesRollsAPlayerWasNeverEntitledTo()
    {
        await using var masterConnection = await ConnectAsync("Master");
        await masterConnection.InvokeAsync<bool>("JoinSession", _openSession);
        await masterConnection.InvokeAsync<bool>("Roll", _openSession, "d20", RollVisibility.MasterOnly);
        await masterConnection.InvokeAsync<bool>("Roll", _openSession, "d6", RollVisibility.Public);

        var history = new TaskCompletionSource<IReadOnlyList<RollLine>>();

        await using var player = await ConnectAsync("Player");
        player.On<IReadOnlyList<RollLine>>("RollHistory", rolls => history.TrySetResult(rolls));
        await player.InvokeAsync<bool>("JoinSession", _openSession);

        var seen = await history.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // A reconnecting player must not learn of what was hidden while they were away.
        Assert.Equal("d6", Assert.Single(seen).Expression);
    }

    [Fact]
    public async Task TheMasterSeesEverythingInHistory()
    {
        await using var player = await ConnectAsync("Player");
        await player.InvokeAsync<bool>("JoinSession", _openSession);
        await player.InvokeAsync<bool>("Roll", _openSession, "d20", RollVisibility.Private);

        var history = new TaskCompletionSource<IReadOnlyList<RollLine>>();

        await using var masterConnection = await ConnectAsync("Master");
        masterConnection.On<IReadOnlyList<RollLine>>("RollHistory", rolls => history.TrySetResult(rolls));
        await masterConnection.InvokeAsync<bool>("JoinSession", _openSession);

        Assert.Single(await history.Task.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task AnUnparseableExpressionIsRefused()
    {
        await using var master = await ConnectAsync("Master");
        await master.InvokeAsync<bool>("JoinSession", _openSession);

        Assert.False(await master.InvokeAsync<bool>("Roll", _openSession, "banana", RollVisibility.Public));
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
