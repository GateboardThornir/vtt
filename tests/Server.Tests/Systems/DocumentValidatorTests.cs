using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Vtt.Server.Systems;

namespace Vtt.Server.Tests.Systems;

public class DocumentValidatorTests
{
    // Resolved through the module's own registration: the implementation stays internal, and
    // going through AddGameSystems also proves it is wired up.
    private readonly IDocumentValidator _validator = new ServiceCollection()
        .AddGameSystems()
        .BuildServiceProvider()
        .GetRequiredService<IDocumentValidator>();

    private static readonly JsonNode _schema = JsonNode.Parse(
        """
        {
          "type": "object",
          "required": ["name", "abilities"],
          "properties": {
            "name": { "type": "string" },
            "size": { "type": "string", "enum": ["small", "medium", "large"] },
            "abilities": {
              "type": "object",
              "required": ["strength"],
              "properties": { "strength": { "type": "integer" } }
            }
          }
        }
        """)!;

    [Fact]
    public void AConformingDocumentValidates()
    {
        var result = Validate("""{"name":"Ireena","size":"medium","abilities":{"strength":14}}""");

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void AMissingRequiredFieldFails()
    {
        var result = Validate("""{"abilities":{"strength":14}}""");

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void AWrongTypeFails()
    {
        // The floor a Master override cannot go through: a hit point total the rules forbid is
        // allowed on purpose, a hit point total of "banana" is not.
        var result = Validate("""{"name":"Ireena","abilities":{"strength":"banana"}}""");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void AValueOutsideAnEnumFails()
    {
        var result = Validate("""{"name":"Ireena","size":"enormous","abilities":{"strength":14}}""");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void AFailureSaysWhereTheProblemIs()
    {
        // "Invalid" tells nobody anything about a nested sheet. The path is what makes it
        // actionable, and it is the reason the error type carries one.
        var result = Validate("""{"name":"Ireena","abilities":{"strength":"banana"}}""");

        Assert.Contains(result.Errors, error => error.Path.Contains("strength", StringComparison.Ordinal));
    }

    [Fact]
    public void AnInvalidDocumentAlwaysReportsAtLeastOneError()
    {
        // A schema can fail a document without producing leaf detail. An empty error list on an
        // invalid result reads as "valid but not", so there is always at least one.
        var permissive = JsonNode.Parse("""{"not": {}}""")!;

        var result = _validator.Validate(permissive, JsonNode.Parse("""{"anything":1}""")!);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void ValidatingRepeatedlyReusesTheCompiledSchema()
    {
        // Not observable directly, so this asserts the behaviour that would break if the cache were
        // keyed wrongly: the same schema must keep giving the same answer.
        for (var attempt = 0; attempt < 50; attempt++)
        {
            Assert.True(Validate("""{"name":"Ireena","abilities":{"strength":14}}""").IsValid);
            Assert.False(Validate("""{"abilities":{"strength":14}}""").IsValid);
        }
    }

    [Fact]
    public void TwoDifferentSchemasDoNotShareACacheEntry()
    {
        var other = JsonNode.Parse("""{"type":"object","required":["other"]}""")!;

        Assert.True(_validator.Validate(other, JsonNode.Parse("""{"other":1}""")!).IsValid);
        Assert.False(Validate("""{"other":1}""").IsValid);
    }

    private DocumentValidation Validate(string json) =>
        _validator.Validate(_schema, JsonNode.Parse(json)!);
}
