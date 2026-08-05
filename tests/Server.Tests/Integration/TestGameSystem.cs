using System.Text.Json.Nodes;
using Vtt.Server.Systems;

namespace Vtt.Server.Tests.Integration;

/// <summary>
/// A minimal module so tests have a pin that resolves.
/// </summary>
/// <remarks>
/// Registered only in the test host. The real 5e module arrives at task 032; until then the
/// application genuinely has no registered system, and campaign creation is unavailable in it —
/// a consequence of 030 landing before 032, recorded rather than papered over.
/// <para>
/// Deliberately not a stand-in for 5e: it exists so that the registry, the pin check and the
/// campaign flow can be exercised without any game's rules leaking into a test fixture.
/// </para>
/// </remarks>
internal sealed class TestGameSystem : IGameSystem
{
    public string SystemId => "dnd5e";

    public string Version => "1.0";

    public JsonNode CharacterSheetSchema { get; } = JsonNode.Parse("""{"type":"object"}""")!;

    public JsonNode CompendiumEntrySchema { get; } = JsonNode.Parse("""{"type":"object"}""")!;

    public SheetDocument RecomputeDerived(SheetDocument sheet) => sheet;

    public SheetDocument MigrateSheet(SheetDocument sheet, string fromVersion) => sheet;
}
