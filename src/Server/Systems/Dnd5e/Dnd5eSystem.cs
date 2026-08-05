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

    /// <remarks>Task 033 fills this in. Returning the sheet untouched until then is honest.</remarks>
    public SheetDocument RecomputeDerived(SheetDocument sheet) => sheet;

    /// <remarks>
    /// There is no earlier version to come from, so every source version is unknown and refused.
    /// Returning the document unchanged would be worse than failing: it would claim a migration had
    /// happened, and the mistake would surface as corrupt data long after the upgrade.
    /// </remarks>
    public SheetDocument MigrateSheet(SheetDocument sheet, string fromVersion) =>
        throw new NotSupportedException(
            $"No migration path from '{fromVersion}' to {SystemId} {Version}.");
}
