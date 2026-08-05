using System.Text.Json.Nodes;

namespace Vtt.Server.Systems;

/// <summary>
/// Everything the platform knows about one version of one game.
/// </summary>
/// <remarks>
/// The core contains no rule from any specific game: no hit points, no armour class, no spell
/// slots. If core code ever does, that is a defect rather than a shortcut — see
/// <c>.claude/rules/game-systems.md</c>, which is binding.
/// <para>
/// <c>Resolve(GameIntent, TableState)</c> is part of the contract and is deliberately absent here.
/// Both of its parameter types are built by tasks 060 and 061, and inventing their shapes six tasks
/// before anything consumes them would be guessing. 061 adds the method alongside the pipeline that
/// calls it; the deviation is recorded in <c>PROGRESS.md</c>.
/// </para>
/// </remarks>
public interface IGameSystem
{
    /// <summary>Stable identifier, e.g. <c>dnd5e</c>. Half of what a campaign pins.</summary>
    string SystemId { get; }

    /// <summary>Semantic version. The other half of the pin, and never changed in place.</summary>
    string Version { get; }

    /// <summary>The schema a character sheet document must satisfy.</summary>
    JsonNode CharacterSheetSchema { get; }

    /// <summary>The schema a compendium entry must satisfy.</summary>
    JsonNode CompendiumEntrySchema { get; }

    /// <summary>
    /// Recomputes every derived value on a sheet.
    /// </summary>
    /// <remarks>
    /// Separate from resolution on purpose: a Master override writes raw fields without passing
    /// through any rule, so this must be called after **every** sheet write, unconditionally. It is
    /// what stops a free edit leaving the derived values disagreeing with the ones they derive from.
    /// </remarks>
    SheetDocument RecomputeDerived(SheetDocument sheet);

    /// <summary>
    /// Moves a sheet written for an older version of this system onto the current one.
    /// </summary>
    /// <remarks>
    /// Nothing calls this until task 080 builds the campaign upgrade flow. It exists now so that
    /// the obligation is visible from the first module: shipping a schema change without a
    /// migration is the one mistake that destroys years of campaign data.
    /// </remarks>
    SheetDocument MigrateSheet(SheetDocument sheet, string fromVersion);
}
