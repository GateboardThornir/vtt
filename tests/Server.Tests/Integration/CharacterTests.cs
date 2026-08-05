using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Vtt.Server.Accounts;
using Vtt.Server.Campaigns;
using Vtt.Server.Characters;
using Vtt.Server.Infrastructure;

namespace Vtt.Server.Tests.Integration;

[Collection(IntegrationDatabase.Name)]
[Trait("Category", "Integration")]
public class CharacterTests(PostgresFixture fixture) : IAsyncLifetime
{
    private const string Password = "a perfectly ordinary passphrase";

    private static readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private const string Sheet =
        """
        {
          "identity": { "name": "Ireena Kolyana", "class": "Fighter", "level": 3 },
          "abilities": {
            "strength": 16, "dexterity": 14, "constitution": 15,
            "intelligence": 10, "wisdom": 12, "charisma": 13
          },
          "proficiencyBonus": 2,
          "hitPoints": { "current": 28, "maximum": 28, "temporary": 0 },
          "armourClass": 16,
          "savingThrowProficiencies": ["strength", "constitution"],
          "skillProficiencies": ["athletics", "perception"]
        }
        """;

    private Guid _campaign;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await CreateAccountAsync("Master");
        await CreateAccountAsync("Player");
        await CreateAccountAsync("Other");
        await CreateAccountAsync("Stranger");

        using var master = await SignedInAsync("Master");
        var created = await master.PostAsJsonAsync(
            "/api/campaigns",
            new CreateCampaignRequest("A campaign", "dnd5e", "1.0.0"));

        _campaign = (await created.Content.ReadFromJsonAsync<CampaignSummary>(_jsonOptions))!.Id;

        foreach (var name in new[] { "Player", "Other" })
        {
            await master.PostAsJsonAsync($"/api/campaigns/{_campaign}/roster", new InviteMemberRequest(name));

            using var member = await SignedInAsync(name);
            await member.PostAsJsonAsync(
                $"/api/campaigns/{_campaign}/roster/response",
                new RespondToInvitationRequest(true));
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task APlayerCreatesACharacterAndOwnsIt()
    {
        using var player = await SignedInAsync("Player");
        var created = await CreateAsync(player, "Ireena", Sheet);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var detail = await created.Content.ReadFromJsonAsync<CharacterDetail>(_jsonOptions);
        Assert.Equal("Ireena", detail?.Name);
    }

    [Fact]
    public async Task TheStoredSheetCarriesTheModulesDerivedValues()
    {
        using var player = await SignedInAsync("Player");
        var detail = await CreatedDetailAsync(player, "Ireena", Sheet);

        var derived = JsonNode.Parse(detail.Sheet)!["derived"]!;

        // Computed by the module, not sent by the client — 033's arithmetic, hand-checked.
        Assert.Equal(3, derived["abilityModifiers"]!["strength"]!.GetValue<int>());
        Assert.Equal(13, derived["passivePerception"]!.GetValue<int>());
    }

    [Fact]
    public async Task DerivedValuesSentByTheClientAreOverwritten()
    {
        // A client may claim whatever it likes about its own modifiers; what is stored is always
        // the module's arithmetic. This is the same discipline as the Master override path.
        var tampered = Sheet.TrimEnd().TrimEnd('}')
            + """, "derived": { "passivePerception": 99, "abilityModifiers": { "strength": 99 } } }""";

        using var player = await SignedInAsync("Player");
        var detail = await CreatedDetailAsync(player, "Ireena", tampered);

        var derived = JsonNode.Parse(detail.Sheet)!["derived"]!;

        Assert.Equal(3, derived["abilityModifiers"]!["strength"]!.GetValue<int>());
        Assert.Equal(13, derived["passivePerception"]!.GetValue<int>());
    }

    [Fact]
    public async Task AnInvalidSheetIsRefusedWithThePathToTheProblem()
    {
        using var player = await SignedInAsync("Player");
        var broken = Sheet.Replace("\"strength\": 16", "\"strength\": \"very\"", StringComparison.Ordinal);

        var response = await CreateAsync(player, "Ireena", broken);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("sheet_invalid", body, StringComparison.Ordinal);
        Assert.Contains("strength", body, StringComparison.Ordinal);

        // Nothing stored: validation happens before anything is written.
        Assert.Empty(await ListAsync(player));
    }

    [Fact]
    public async Task TheSheetRoundTripsThroughJsonbUnchangedApartFromDerived()
    {
        using var player = await SignedInAsync("Player");
        var detail = await CreatedDetailAsync(player, "Ireena", Sheet);

        var stored = JsonNode.Parse(detail.Sheet)!.AsObject();
        var original = JsonNode.Parse(Sheet)!.AsObject();

        foreach (var (key, value) in original)
        {
            Assert.Equal(value!.ToJsonString(), stored[key]!.ToJsonString());
        }
    }

    [Fact]
    public async Task TheMasterCanEditAnyCharacterInTheirCampaign()
    {
        using var player = await SignedInAsync("Player");
        var character = await CreatedDetailAsync(player, "Ireena", Sheet);

        using var master = await SignedInAsync("Master");
        var response = await master.PutAsJsonAsync(
            $"/api/campaigns/{_campaign}/characters/{character.Id}",
            new SaveCharacterRequest("Ireena the Blessed", Sheet));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task APlayerCannotEditSomebodyElsesCharacter()
    {
        using var player = await SignedInAsync("Player");
        var character = await CreatedDetailAsync(player, "Ireena", Sheet);

        using var other = await SignedInAsync("Other");
        var response = await other.PutAsJsonAsync(
            $"/api/campaigns/{_campaign}/characters/{character.Id}",
            new SaveCharacterRequest("Stolen", Sheet));

        // On the roster, so they know the character exists: 403, not 404.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AStrangerSeesNothing()
    {
        using var player = await SignedInAsync("Player");
        var character = await CreatedDetailAsync(player, "Ireena", Sheet);

        using var stranger = await SignedInAsync("Stranger");

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await stranger.GetAsync(new Uri($"/api/campaigns/{_campaign}/characters", UriKind.Relative))).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await stranger.GetAsync(
                new Uri($"/api/campaigns/{_campaign}/characters/{character.Id}", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task EveryMemberOfTheCampaignCanSeeTheCharacterList()
    {
        using var player = await SignedInAsync("Player");
        await CreateAsync(player, "Ireena", Sheet);

        using var other = await SignedInAsync("Other");
        Assert.Single(await ListAsync(other));
    }

    [Fact]
    public async Task UpdatingRecomputesDerivedValuesAgain()
    {
        using var player = await SignedInAsync("Player");
        var character = await CreatedDetailAsync(player, "Ireena", Sheet);

        var stronger = Sheet.Replace("\"strength\": 16", "\"strength\": 20", StringComparison.Ordinal);

        var response = await player.PutAsJsonAsync(
            $"/api/campaigns/{_campaign}/characters/{character.Id}",
            new SaveCharacterRequest("Ireena", stronger));

        var updated = await response.Content.ReadFromJsonAsync<CharacterDetail>(_jsonOptions);
        var derived = JsonNode.Parse(updated!.Sheet)!["derived"]!;

        // RecomputeDerived runs on every write, not only on create.
        Assert.Equal(5, derived["abilityModifiers"]!["strength"]!.GetValue<int>());
    }

    private Task<HttpResponseMessage> CreateAsync(HttpClient client, string name, string sheet) =>
        client.PostAsJsonAsync(
            $"/api/campaigns/{_campaign}/characters",
            new SaveCharacterRequest(name, sheet));

    private async Task<CharacterDetail> CreatedDetailAsync(HttpClient client, string name, string sheet)
    {
        var response = await CreateAsync(client, name, sheet);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<CharacterDetail>(_jsonOptions))!;
    }

    private async Task<List<CharacterSummary>> ListAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<CharacterSummary>>(
            $"/api/campaigns/{_campaign}/characters",
            _jsonOptions))!;

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
