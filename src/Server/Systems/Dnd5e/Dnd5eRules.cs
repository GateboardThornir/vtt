namespace Vtt.Server.Systems.Dnd5e;

/// <summary>
/// The arithmetic of 5e, stated once each.
/// </summary>
/// <remarks>
/// Every rule in this file belongs to D&amp;D 5e and none of it may leak toward the core — a rule
/// from a specific game living outside a module is a defect, per
/// <c>.claude/rules/game-systems.md</c>.
/// </remarks>
internal static class Dnd5eRules
{
    public static readonly string[] Abilities =
    [
        "strength", "dexterity", "constitution", "intelligence", "wisdom", "charisma",
    ];

    /// <summary>Each skill and the ability it is rolled against.</summary>
    public static readonly IReadOnlyDictionary<string, string> Skills = new Dictionary<string, string>
    {
        ["acrobatics"] = "dexterity",
        ["animalHandling"] = "wisdom",
        ["arcana"] = "intelligence",
        ["athletics"] = "strength",
        ["deception"] = "charisma",
        ["history"] = "intelligence",
        ["insight"] = "wisdom",
        ["intimidation"] = "charisma",
        ["investigation"] = "intelligence",
        ["medicine"] = "wisdom",
        ["nature"] = "intelligence",
        ["perception"] = "wisdom",
        ["performance"] = "charisma",
        ["persuasion"] = "charisma",
        ["religion"] = "intelligence",
        ["sleightOfHand"] = "dexterity",
        ["stealth"] = "dexterity",
        ["survival"] = "wisdom",
    };

    /// <summary>
    /// The modifier for an ability score.
    /// </summary>
    /// <remarks>
    /// Floor division, not C#'s integer division. <c>(7 - 10) / 2</c> truncates toward zero and
    /// gives −1; the rule is −2. This is the classic bug in this function, and the reason the
    /// negative half is written out rather than left to the language.
    /// </remarks>
    public static int AbilityModifier(int score)
    {
        var difference = score - 10;

        return difference >= 0
            ? difference / 2
            : (difference - 1) / 2;
    }

    public static int PassivePerception(int perceptionTotal) => 10 + perceptionTotal;
}
