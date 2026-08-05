using System.Text.Json.Nodes;

namespace Vtt.Server.Systems.Dnd5e;

/// <summary>
/// D&amp;D 5th Edition, built on SRD-licensed content only.
/// </summary>
/// <remarks>
/// The first shipped module. No SRD text lives in this assembly: the schemas describe shapes, and
/// content arrives through task 078's offline ingestion pipeline.
/// </remarks>
internal sealed class Dnd5eSystem : IGameSystem
{
    public string SystemId => "dnd5e";

    /// <remarks>
    /// A full semantic version, not <c>1.0</c>. The pin is a contract, and a database holding three
    /// test rows is the cheapest moment this will ever be to get right.
    /// </remarks>
    public string Version => "1.0.0";

    public JsonNode CharacterSheetSchema { get; } = JsonNode.Parse(Dnd5eSchemas.CharacterSheet)!;

    public JsonNode CompendiumEntrySchema { get; } = JsonNode.Parse(Dnd5eSchemas.CompendiumEntry)!;

    /// <remarks>
    /// Pure and total: it returns a new document, never mutates the one it was given, and never
    /// half-fills <c>derived</c> — partially computed values are worse than none, because they look
    /// authoritative.
    /// <para>
    /// <c>derived</c> is replaced outright rather than merged. Whatever was there was either
    /// computed by a previous call or written by hand, and neither is a reason to keep it. That is
    /// precisely what makes the Master override path safe: an override writes raw fields, this runs
    /// afterwards unconditionally, and the derived values cannot drift from what they derive from.
    /// </para>
    /// </remarks>
    public SheetDocument RecomputeDerived(SheetDocument sheet)
    {
        var copy = sheet.DeepCopy();
        var root = copy.Root;

        var abilities = root["abilities"]?.AsObject();

        if (abilities is null)
        {
            // Nothing to derive from. The schema requires abilities, so this is only reachable for
            // a document that has not been validated — returning it untouched is honest.
            return copy;
        }

        var proficiencyBonus = root["proficiencyBonus"]?.GetValue<int>() ?? 0;

        var modifiers = new JsonObject();

        foreach (var ability in Dnd5eRules.Abilities)
        {
            modifiers[ability] = Dnd5eRules.AbilityModifier(abilities[ability]?.GetValue<int>() ?? 10);
        }

        var proficientSaves = Names(root["savingThrowProficiencies"]);
        var saves = new JsonObject();

        foreach (var ability in Dnd5eRules.Abilities)
        {
            saves[ability] = modifiers[ability]!.GetValue<int>()
                + (proficientSaves.Contains(ability) ? proficiencyBonus : 0);
        }

        var proficientSkills = Names(root["skillProficiencies"]);
        var skills = new JsonObject();

        foreach (var (skill, ability) in Dnd5eRules.Skills)
        {
            skills[skill] = modifiers[ability]!.GetValue<int>()
                + (proficientSkills.Contains(skill) ? proficiencyBonus : 0);
        }

        root["derived"] = new JsonObject
        {
            ["abilityModifiers"] = modifiers,
            ["savingThrows"] = saves,
            ["skills"] = skills,
            ["passivePerception"] = Dnd5eRules.PassivePerception(skills["perception"]!.GetValue<int>()),
        };

        return copy;
    }

    private static HashSet<string> Names(JsonNode? array) =>
        array?.AsArray().Select(entry => entry?.GetValue<string>() ?? string.Empty).ToHashSet(StringComparer.Ordinal)
        ?? new HashSet<string>(StringComparer.Ordinal);

    /// <remarks>
    /// There is no earlier version to come from, so every source version is unknown and refused.
    /// Returning the document unchanged would be worse than failing: it would claim a migration had
    /// happened, and the mistake would surface as corrupt data long after the upgrade.
    /// </remarks>
    public SheetDocument MigrateSheet(SheetDocument sheet, string fromVersion) =>
        throw new NotSupportedException(
            $"No migration path from '{fromVersion}' to {SystemId} {Version}.");
}
