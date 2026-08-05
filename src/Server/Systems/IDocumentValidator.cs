using System.Text.Json.Nodes;

namespace Vtt.Server.Systems;

/// <summary>
/// Checks a document against the schema its module declares.
/// </summary>
/// <remarks>
/// Shape only, and the distinction is the whole point. <c>.claude/rules/game-systems.md</c> is
/// explicit that a Master override bypasses **rule** validation but never **schema** validation:
/// the Master may set a hit point total the rules forbid, deliberately, and may not set it to
/// <c>"banana"</c>. This is that floor.
/// <para>
/// If this type ever learns what a legal hit point total is, the floor has become a ceiling and the
/// override path has stopped working as designed.
/// </para>
/// </remarks>
public interface IDocumentValidator
{
    DocumentValidation Validate(JsonNode schema, JsonNode document);

    /// <summary>Validates a sheet against the character sheet schema of a module.</summary>
    DocumentValidation ValidateSheet(IGameSystem system, SheetDocument sheet);
}
