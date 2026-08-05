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
public class RosterTests(PostgresFixture fixture) : IAsyncLifetime
{
    private const string Password = "a perfectly ordinary passphrase";

    private static readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await CreateAccountAsync("Master");
        await CreateAccountAsync("Player");
        await CreateAccountAsync("Stranger");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreatingACampaignPutsTheMasterOnItsRoster()
    {
        using var master = await SignedInAsync("Master");
        var campaign = await CreateCampaignAsync(master);

        var roster = await master.GetFromJsonAsync<List<RosterEntry>>(
            $"/api/campaigns/{campaign}/roster",
            _jsonOptions);

        var entry = Assert.Single(roster!);
        Assert.Equal("Master", entry.Username);
        Assert.Equal(CampaignRole.Master, entry.Role);
        Assert.Equal(MembershipState.Active, entry.State);
    }

    [Fact]
    public async Task AnInvitationConfersNothingUntilItIsAccepted()
    {
        // The mistake this guards against is treating any row in the membership table as
        // membership. An invited account must not see the campaign's content.
        using var master = await SignedInAsync("Master");
        var campaign = await CreateCampaignAsync(master);
        await InviteAsync(master, campaign, "Player");

        using var player = await SignedInAsync("Player");

        Assert.Empty((await player.GetFromJsonAsync<List<CampaignSummary>>("/api/campaigns", _jsonOptions))!);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await player.GetAsync(new Uri($"/api/campaigns/{campaign}", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task AnInvitationIsVisibleToItsRecipient()
    {
        using var master = await SignedInAsync("Master");
        var campaign = await CreateCampaignAsync(master);
        await InviteAsync(master, campaign, "Player");

        using var player = await SignedInAsync("Player");
        var invitations = await player.GetFromJsonAsync<List<CampaignSummary>>(
            "/api/campaigns/invitations",
            _jsonOptions);

        Assert.Equal(campaign, Assert.Single(invitations!).Id);
    }

    [Fact]
    public async Task AcceptingAnInvitationGrantsVisibility()
    {
        using var master = await SignedInAsync("Master");
        var campaign = await CreateCampaignAsync(master);
        await InviteAsync(master, campaign, "Player");

        using var player = await SignedInAsync("Player");
        Assert.Equal(HttpStatusCode.NoContent, (await RespondAsync(player, campaign, accept: true)).StatusCode);

        var visible = await player.GetFromJsonAsync<List<CampaignSummary>>("/api/campaigns", _jsonOptions);
        Assert.Equal(CampaignRole.Player, Assert.Single(visible!).Role);
    }

    [Fact]
    public async Task DecliningLeavesTheCampaignInvisible()
    {
        using var master = await SignedInAsync("Master");
        var campaign = await CreateCampaignAsync(master);
        await InviteAsync(master, campaign, "Player");

        using var player = await SignedInAsync("Player");
        await RespondAsync(player, campaign, accept: false);

        Assert.Empty((await player.GetFromJsonAsync<List<CampaignSummary>>("/api/campaigns", _jsonOptions))!);
    }

    [Fact]
    public async Task LeavingRemovesVisibilityImmediately()
    {
        using var master = await SignedInAsync("Master");
        var campaign = await CreateCampaignAsync(master);
        await InviteAsync(master, campaign, "Player");

        using var player = await SignedInAsync("Player");
        await RespondAsync(player, campaign, accept: true);
        Assert.Single((await player.GetFromJsonAsync<List<CampaignSummary>>("/api/campaigns", _jsonOptions))!);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await player.DeleteAsync(new Uri($"/api/campaigns/{campaign}/roster/me", UriKind.Relative))).StatusCode);

        // No cookie refresh, no sign-out: the roster is read on every request, so the change lands
        // at once. The same property task 016 established for platform roles.
        Assert.Empty((await player.GetFromJsonAsync<List<CampaignSummary>>("/api/campaigns", _jsonOptions))!);
    }

    [Fact]
    public async Task TheMasterCannotLeaveTheirOwnCampaign()
    {
        using var master = await SignedInAsync("Master");
        var campaign = await CreateCampaignAsync(master);

        var response = await master.DeleteAsync(new Uri($"/api/campaigns/{campaign}/roster/me", UriKind.Relative));

        // A campaign with no Master has nobody who can run it and nobody who can hand it over.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task APlayerCannotInviteAnybody()
    {
        using var master = await SignedInAsync("Master");
        var campaign = await CreateCampaignAsync(master);
        await InviteAsync(master, campaign, "Player");

        using var player = await SignedInAsync("Player");
        await RespondAsync(player, campaign, accept: true);

        var response = await InviteAsync(player, campaign, "Stranger");

        // On the roster, so they already know the campaign exists: 403 rather than 404.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AStrangerGetsNotFoundRatherThanForbidden()
    {
        using var master = await SignedInAsync("Master");
        var campaign = await CreateCampaignAsync(master);

        using var stranger = await SignedInAsync("Stranger");

        Assert.Equal(HttpStatusCode.NotFound, (await InviteAsync(stranger, campaign, "Player")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await stranger.GetAsync(new Uri($"/api/campaigns/{campaign}/roster", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task TheMasterCanRemoveAPlayer()
    {
        using var master = await SignedInAsync("Master");
        var campaign = await CreateCampaignAsync(master);
        await InviteAsync(master, campaign, "Player");

        using var player = await SignedInAsync("Player");
        await RespondAsync(player, campaign, accept: true);

        var playerId = (await master.GetFromJsonAsync<List<RosterEntry>>(
            $"/api/campaigns/{campaign}/roster",
            _jsonOptions))!.Single(entry => entry.Username == "Player").UserId;

        var response = await master.DeleteAsync(
            new Uri($"/api/campaigns/{campaign}/roster/{playerId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty((await player.GetFromJsonAsync<List<CampaignSummary>>("/api/campaigns", _jsonOptions))!);
    }

    [Fact]
    public async Task InvitingTheSameAccountTwiceDoesNotCreateASecondRow()
    {
        using var master = await SignedInAsync("Master");
        var campaign = await CreateCampaignAsync(master);

        await InviteAsync(master, campaign, "Player");
        var second = await InviteAsync(master, campaign, "Player");

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var roster = await master.GetFromJsonAsync<List<RosterEntry>>(
            $"/api/campaigns/{campaign}/roster",
            _jsonOptions);

        Assert.Equal(2, roster!.Count);
    }

    [Fact]
    public async Task SomebodyWhoLeftCanBeInvitedBackAgain()
    {
        using var master = await SignedInAsync("Master");
        var campaign = await CreateCampaignAsync(master);
        await InviteAsync(master, campaign, "Player");

        using var player = await SignedInAsync("Player");
        await RespondAsync(player, campaign, accept: true);
        await player.DeleteAsync(new Uri($"/api/campaigns/{campaign}/roster/me", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NoContent, (await InviteAsync(master, campaign, "Player")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await RespondAsync(player, campaign, accept: true)).StatusCode);
        Assert.Single((await player.GetFromJsonAsync<List<CampaignSummary>>("/api/campaigns", _jsonOptions))!);
    }

    [Fact]
    public async Task InvitingAnAccountThatDoesNotExistIsRefused()
    {
        using var master = await SignedInAsync("Master");
        var campaign = await CreateCampaignAsync(master);

        var response = await InviteAsync(master, campaign, "NobodyAtAll");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static Task<HttpResponseMessage> InviteAsync(HttpClient client, Guid campaign, string username) =>
        client.PostAsJsonAsync($"/api/campaigns/{campaign}/roster", new InviteMemberRequest(username));

    private static Task<HttpResponseMessage> RespondAsync(HttpClient client, Guid campaign, bool accept) =>
        client.PostAsJsonAsync(
            $"/api/campaigns/{campaign}/roster/response",
            new RespondToInvitationRequest(accept));

    private static async Task<Guid> CreateCampaignAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/campaigns",
            new CreateCampaignRequest("A campaign", "dnd5e", "1.0"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

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
