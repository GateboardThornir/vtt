using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Vtt.Server.Systems;

namespace Vtt.Server.Tests.Systems;

public class Dnd5eRecomputeTests
{
    private readonly IServiceProvider _services = new ServiceCollection()
        .AddGameSystems()
        .BuildServiceProvider();

    private IGameSystem Module =>
        _services.GetRequiredService<IGameSystemRegistry>().Find("dnd5e", "1.0.0")!;

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

    [Theory]
    // The standard table, hand-checked. The odd scores are the interesting ones, and the values
    // below 10 are where truncation toward zero would give the wrong answer.
    [InlineData(1, -5)]
    [InlineData(3, -4)]
    [InlineData(7, -2)]
    [InlineData(8, -1)]
    [InlineData(9, -1)]
    [InlineData(10, 0)]
    [InlineData(11, 0)]
    [InlineData(12, 1)]
    [InlineData(15, 2)]
    [InlineData(16, 3)]
    [InlineData(20, 5)]
    [InlineData(30, 10)]
    public void ModifiersMatchTheStandardTable(int score, int expected)
    {
        var sheet = Recompute(Ireena.Replace("\"strength\": 16", $"\"strength\": {score}", StringComparison.Ordinal));

        Assert.Equal(expected, Derived(sheet, "abilityModifiers")["strength"]!.GetValue<int>());
    }

    [Fact]
    public void AScoreBelowTenGivesANegativeModifier()
    {
        // C# integer division truncates toward zero: (7 - 10) / 2 is -1, and the rule is -2. This
        // is the classic bug in this function.
        var sheet = Recompute(Ireena.Replace("\"strength\": 16", "\"strength\": 7", StringComparison.Ordinal));

        Assert.Equal(-2, Derived(sheet, "abilityModifiers")["strength"]!.GetValue<int>());
    }

    [Fact]
    public void AProficientSavingThrowIncludesTheBonus()
    {
        var saves = Derived(Recompute(Ireena), "savingThrows");

        Assert.Equal(5, saves["strength"]!.GetValue<int>());       // 16 is +3, proficient (+2)
        Assert.Equal(4, saves["constitution"]!.GetValue<int>());   // 15 is +2, proficient (+2)
    }

    [Fact]
    public void ANonProficientSavingThrowDoesNot()
    {
        var saves = Derived(Recompute(Ireena), "savingThrows");

        // Dexterity 14 is +2, not proficient.
        Assert.Equal(2, saves["dexterity"]!.GetValue<int>());
        Assert.Equal(0, saves["intelligence"]!.GetValue<int>());
    }

    [Fact]
    public void EveryOneOfTheEighteenSkillsIsComputed()
    {
        var skills = Derived(Recompute(Ireena), "skills");

        Assert.Equal(18, skills.Count);
    }

    [Fact]
    public void EachSkillUsesItsGoverningAbility()
    {
        var skills = Derived(Recompute(Ireena), "skills");

        Assert.Equal(5, skills["athletics"]!.GetValue<int>());     // strength +3, proficient
        Assert.Equal(2, skills["acrobatics"]!.GetValue<int>());    // dexterity +2
        Assert.Equal(0, skills["arcana"]!.GetValue<int>());        // intelligence +0
        Assert.Equal(1, skills["insight"]!.GetValue<int>());       // wisdom +1
        Assert.Equal(1, skills["persuasion"]!.GetValue<int>());    // charisma +1
    }

    [Fact]
    public void PassivePerceptionAccountsForProficiency()
    {
        // Wisdom 12 is +1, perception proficient (+2), so 10 + 3.
        Assert.Equal(13, Recompute(Ireena).Root["derived"]!["passivePerception"]!.GetValue<int>());
    }

    [Fact]
    public void TheInputDocumentIsNotMutated()
    {
        var original = SheetDocument.Parse(Ireena);

        Module.RecomputeDerived(original);

        Assert.Null(original.Root["derived"]);
    }

    [Fact]
    public void DerivedValuesWrittenByHandAreReplaced()
    {
        // The reason derived is overwritten rather than merged: a hand-edited value would otherwise
        // survive and leave the sheet permanently disagreeing with itself. This is what makes the
        // Master override path safe.
        var tampered = Ireena.TrimEnd().TrimEnd('}')
            + """, "derived": { "passivePerception": 99, "abilityModifiers": { "strength": 99 } } }""";

        var sheet = Recompute(tampered);

        Assert.Equal(3, Derived(sheet, "abilityModifiers")["strength"]!.GetValue<int>());
        Assert.Equal(13, sheet.Root["derived"]!["passivePerception"]!.GetValue<int>());
    }

    [Fact]
    public void TheResultStillValidatesAgainstTheSchema()
    {
        var validator = _services.GetRequiredService<IDocumentValidator>();

        var result = validator.ValidateSheet(Module, Recompute(Ireena));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(error => $"{error.Path}: {error.Message}")));
    }

    private SheetDocument Recompute(string json) => Module.RecomputeDerived(SheetDocument.Parse(json));

    private static JsonObject Derived(SheetDocument sheet, string section) =>
        sheet.Root["derived"]![section]!.AsObject();
}
