using System.Text.Json;
using System.Text.Json.Nodes;

namespace Vtt.Server.Systems;

/// <summary>
/// A character sheet, whose shape is defined by a game system rather than by the platform.
/// </summary>
/// <remarks>
/// A document rather than a typed model, because the platform cannot know what a sheet contains —
/// that is the whole point of the system contract. It is stored as JSONB and validated against the
/// pinned module's schema before it is persisted.
/// </remarks>
public sealed record SheetDocument(JsonObject Root)
{
    public static SheetDocument Parse(string json) =>
        new(JsonNode.Parse(json)?.AsObject() ?? throw new JsonException("A sheet must be a JSON object."));

    /// <summary>An independent copy, so a transform cannot mutate the caller's document.</summary>
    public SheetDocument DeepCopy() => new(Root.DeepClone().AsObject());

    public override string ToString() => Root.ToJsonString();
}
