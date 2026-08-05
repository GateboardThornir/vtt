using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Vtt.Server.Accounts;
using Vtt.Server.Campaigns;
using Vtt.Server.Infrastructure;
using Vtt.Server.Notifications;

namespace Vtt.Server.Tests.Integration;

[Collection(IntegrationDatabase.Name)]
[Trait("Category", "Integration")]
public class NotificationTests(PostgresFixture fixture) : IAsyncLifetime
{
    private const string Password = "a perfectly ordinary passphrase";

    private static readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await CreateAccountAsync("Master", admin: false);
        await CreateAccountAsync("Player", admin: false);
        await CreateAccountAsync("Boss", admin: true);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AnInvitationNotifiesTheInvitee()
    {
        using var master = await SignedInAsync("Master");
        var campaign = await CreateCampaignAsync(master, "Curse of Strahd");
        await master.PostAsJsonAsync($"/api/campaigns/{campaign}/roster", new InviteMemberRequest("Player"));

        using var player = await SignedInAsync("Player");
        var mine = await player.GetFromJsonAsync<List<NotificationView>>("/api/notifications", _jsonOptions);

        var notification = Assert.Single(mine!);
        Assert.Equal(NotificationKind.CampaignInvitation, notification.Kind);
        Assert.Equal("Curse of Strahd", notification.Subject);
        Assert.False(notification.Read);
    }

    [Fact]
    public async Task ANotificationCarriesAKindAndNotASentence()
    {
        // The interface is bilingual. A sentence composed on the server arrives untranslatable, so
        // the payload is a kind plus its one variable part and the client renders it.
        using var master = await SignedInAsync("Master");
        var campaign = await CreateCampaignAsync(master, "Curse of Strahd");
        await master.PostAsJsonAsync($"/api/campaigns/{campaign}/roster", new InviteMemberRequest("Player"));

        using var player = await SignedInAsync("Player");
        var body = await (await player.GetAsync(new Uri("/api/notifications", UriKind.Relative)))
            .Content.ReadAsStringAsync();

        Assert.Contains("CampaignInvitation", body, StringComparison.Ordinal);
        Assert.DoesNotContain("invited you", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ha invitato", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TheInviterIsNotNotified()
    {
        using var master = await SignedInAsync("Master");
        var campaign = await CreateCampaignAsync(master, "Curse of Strahd");
        await master.PostAsJsonAsync($"/api/campaigns/{campaign}/roster", new InviteMemberRequest("Player"));

        var mine = await master.GetFromJsonAsync<List<NotificationView>>("/api/notifications", _jsonOptions);

        Assert.Empty(mine!);
    }

    [Fact]
    public async Task ApprovalAndRejectionNotifyTheApplicant()
    {
        var applicant = await CreatePendingAsync("Newcomer");

        using var boss = await SignedInAsync("Boss");
        await boss.PutAsJsonAsync($"/api/admin/accounts/{applicant}/state", new SetAccountStateRequest(AccountState.Active));

        using var newcomer = await SignedInAsync("Newcomer");
        var mine = await newcomer.GetFromJsonAsync<List<NotificationView>>("/api/notifications", _jsonOptions);

        Assert.Equal(NotificationKind.AccountApproved, Assert.Single(mine!).Kind);
    }

    [Fact]
    public async Task OneAccountNeverSeesAnothersNotifications()
    {
        using var master = await SignedInAsync("Master");
        var campaign = await CreateCampaignAsync(master, "Curse of Strahd");
        await master.PostAsJsonAsync($"/api/campaigns/{campaign}/roster", new InviteMemberRequest("Player"));

        using var boss = await SignedInAsync("Boss");
        var theirs = await boss.GetFromJsonAsync<List<NotificationView>>("/api/notifications", _jsonOptions);

        // A platform administrator is not exempt: notifications are strictly per-recipient.
        Assert.Empty(theirs!);
    }

    [Fact]
    public async Task MarkingSomebodyElsesNotificationReadIsNotFoundAndChangesNothing()
    {
        using var master = await SignedInAsync("Master");
        var campaign = await CreateCampaignAsync(master, "Curse of Strahd");
        await master.PostAsJsonAsync($"/api/campaigns/{campaign}/roster", new InviteMemberRequest("Player"));

        using var player = await SignedInAsync("Player");
        var id = (await player.GetFromJsonAsync<List<NotificationView>>("/api/notifications", _jsonOptions))!
            .Single().Id;

        using var boss = await SignedInAsync("Boss");
        var response = await boss.PostAsJsonAsync($"/api/notifications/{id}/read", new { });

        // Knowing the identifier must not be enough to write to another account's row.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var still = (await player.GetFromJsonAsync<List<NotificationView>>("/api/notifications", _jsonOptions))!.Single();
        Assert.False(still.Read);
    }

    [Fact]
    public async Task MarkingReadWorksForTheRecipient()
    {
        using var master = await SignedInAsync("Master");
        var campaign = await CreateCampaignAsync(master, "Curse of Strahd");
        await master.PostAsJsonAsync($"/api/campaigns/{campaign}/roster", new InviteMemberRequest("Player"));

        using var player = await SignedInAsync("Player");
        var id = (await player.GetFromJsonAsync<List<NotificationView>>("/api/notifications", _jsonOptions))!
            .Single().Id;

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await player.PostAsJsonAsync($"/api/notifications/{id}/read", new { })).StatusCode);

        Assert.True((await player.GetFromJsonAsync<List<NotificationView>>("/api/notifications", _jsonOptions))!
            .Single().Read);
    }

    [Fact]
    public async Task MarkingAllReadClearsOnlyYourOwn()
    {
        using var master = await SignedInAsync("Master");
        var campaign = await CreateCampaignAsync(master, "Curse of Strahd");
        await master.PostAsJsonAsync($"/api/campaigns/{campaign}/roster", new InviteMemberRequest("Player"));

        var applicant = await CreatePendingAsync("Newcomer");
        using var boss = await SignedInAsync("Boss");
        await boss.PutAsJsonAsync($"/api/admin/accounts/{applicant}/state", new SetAccountStateRequest(AccountState.Active));

        using var player = await SignedInAsync("Player");
        await player.PostAsJsonAsync("/api/notifications/read", new { });

        Assert.True((await player.GetFromJsonAsync<List<NotificationView>>("/api/notifications", _jsonOptions))!
            .All(notification => notification.Read));

        using var newcomer = await SignedInAsync("Newcomer");
        Assert.False((await newcomer.GetFromJsonAsync<List<NotificationView>>("/api/notifications", _jsonOptions))!
            .Single().Read);
    }

    [Fact]
    public async Task AnAnonymousCallerIsRefused()
    {
        using var client = fixture.CreateClient();

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync(new Uri("/api/notifications", UriKind.Relative))).StatusCode);
    }

    private static async Task<Guid> CreateCampaignAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync(
            "/api/campaigns",
            new CreateCampaignRequest(name, "dnd5e", "1.0.0"));

        return (await response.Content.ReadFromJsonAsync<CampaignSummary>(_jsonOptions))!.Id;
    }

    private async Task<HttpClient> SignedInAsync(string username)
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/session", new SignInRequest(username, Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return client;
    }

    private async Task<Guid> CreateAccountAsync(string username, bool admin)
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

        return user.Id;
    }

    private async Task<Guid> CreatePendingAsync(string username)
    {
        await using var scope = new Scope(fixture.Factory.Services.CreateScope());
        var hash = scope.Provider.GetRequiredService<IPasswordHasher>().Hash(Password);
        var user = User.Register(username, hash, fixture.Clock.GetUtcNow());

        scope.Context.Set<User>().Add(user);
        await scope.Context.SaveChangesAsync();

        return user.Id;
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
