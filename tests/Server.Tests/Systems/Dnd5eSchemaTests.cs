using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Vtt.Server.Systems;

namespace Vtt.Server.Tests.Systems;

public class Dnd5eSchemaTests
{
    private readonly IServiceProvider _services = new ServiceCollection()
        .AddGameSystems()
        .BuildServiceProvider();

    private IGameSystem Module =>
        _services.GetRequiredService<IGameSystemRegistry>().Find("dnd5e", "1.0.0")
        ?? throw new InvalidOperationException("The 5e module is not registered.");

    private IDocumentValidator Validator => _services.GetRequiredService<IDocumentValidator>();

    private const string Ireena =
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

    [Fact]
    public void TheModuleResolvesAtItsPinnedVersion()
    {
        Assert.Equal("dnd5e", Module.SystemId);
        Assert.Equal("1.0.0", Module.Version);
    }

    [Fact]
    public void ARealisticCharacterValidates() => Assert.True(Validate(Ireena).IsValid);

    [Fact]
    public void DerivedValuesAreOptionalOnInput()
    {
        // A sheet arrives without them and RecomputeDerived fills them in — task 033. Requiring
        // them here would mean a client had to compute the rules before the server did.
        Assert.True(Validate(Ireena).IsValid);
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("abilities")]
    [InlineData("proficiencyBonus")]
    [InlineData("hitPoints")]
    [InlineData("armourClass")]
    public void EveryRequiredSectionIsEnforced(string section)
    {
        var node = JsonNode.Parse(Ireena)!.AsObject();
        node.Remove(section);

        Assert.False(Validator.Validate(Module.CharacterSheetSchema, node).IsValid);
    }

    [Fact]
    public void EveryAbilityIsRequired()
    {
        var node = JsonNode.Parse(Ireena)!.AsObject();
        node["abilities"]!.AsObject().Remove("wisdom");

        Assert.False(Validator.Validate(Module.CharacterSheetSchema, node).IsValid);
    }

    [Theory]
    [InlineData("\"strong\"")]
    [InlineData("0")]
    [InlineData("31")]
    [InlineData("14.5")]
    public void AnAbilityScoreOutsideItsRangeOrTypeFails(string value)
    {
        var json = Ireena.Replace("\"strength\": 16", $"\"strength\": {value}", StringComparison.Ordinal);

        Assert.False(Validate(json).IsValid);
    }

    [Fact]
    public void AnUnknownSkillFails()
    {
        var json = Ireena.Replace("\"athletics\"", "\"basketWeaving\"", StringComparison.Ordinal);

        Assert.False(Validate(json).IsValid);
    }

    [Fact]
    public void AnUnknownFieldFails()
    {
        // additionalProperties is false throughout: a typo must be a failure rather than a value
        // silently stored and never read.
        var json = Ireena.Replace("\"armourClass\": 16", "\"armourClass\": 16, \"armorClass\": 16", StringComparison.Ordinal);

        Assert.False(Validate(json).IsValid);
    }

    [Fact]
    public void ADuplicateSkillFails()
    {
        var json = Ireena.Replace(
            "[\"athletics\", \"perception\"]",
            "[\"athletics\", \"athletics\"]",
            StringComparison.Ordinal);

        Assert.False(Validate(json).IsValid);
    }

    [Fact]
    public void MigrationRefusesAVersionItDoesNotKnow()
    {
        // Returning the document unchanged would claim a migration had happened, and the mistake
        // would surface as corrupt data long after the upgrade.
        Assert.Throws<NotSupportedException>(
            () => Module.MigrateSheet(SheetDocument.Parse(Ireena), "0.9.0"));
    }

    [Fact]
    public void TheCompendiumSchemaAcceptsAMinimalEntry()
    {
        var entry = JsonNode.Parse("""{"kind":"spell","name":"Light"}""")!;

        Assert.True(Validator.Validate(Module.CompendiumEntrySchema, entry).IsValid);
    }

    private DocumentValidation Validate(string json) =>
        Validator.Validate(Module.CharacterSheetSchema, JsonNode.Parse(json)!);
}
