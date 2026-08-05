using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Json.Schema;

namespace Vtt.Server.Systems;

internal sealed class DocumentValidator : IDocumentValidator
{
    // A module's schema is a property of a released version and never changes at runtime, so it is
    // compiled on first use and kept. Recompiling per validation would put a parse on the path of
    // every sheet write.
    private readonly ConcurrentDictionary<string, JsonSchema> _compiled = new(StringComparer.Ordinal);

    public DocumentValidation Validate(JsonNode schema, JsonNode document)
    {
        var compiled = _compiled.GetOrAdd(
            schema.ToJsonString(),
            json => JsonSchema.FromText(json));

        // Evaluate takes a JsonElement, so the node is serialised through one. The document has
        // already been parsed once by the caller; this is the cost of the library's surface.
        using var element = System.Text.Json.JsonDocument.Parse(document.ToJsonString());

        var result = compiled.Evaluate(
            element.RootElement,
            new EvaluationOptions { OutputFormat = OutputFormat.List });

        if (result.IsValid)
        {
            return DocumentValidation.Valid;
        }

        var errors = Flatten(result).ToList();

        // A schema can report a failure with no leaf detail; saying so beats an empty list that
        // reads as "valid but not".
        return DocumentValidation.Invalid(
            errors.Count > 0 ? errors : [new DocumentError("/", "The document does not match the schema.")]);
    }

    public DocumentValidation ValidateSheet(IGameSystem system, SheetDocument sheet) =>
        Validate(system.CharacterSheetSchema, sheet.Root);

    private static IEnumerable<DocumentError> Flatten(EvaluationResults results)
    {
        if (!results.IsValid && results.Errors is { Count: > 0 })
        {
            foreach (var error in results.Errors)
            {
                yield return new DocumentError(
                    results.InstanceLocation.ToString() is { Length: > 0 } path ? path : "/",
                    error.Value);
            }
        }

        foreach (var nested in (results.Details ?? []).SelectMany(Flatten))
        {
            yield return nested;
        }
    }
}
