namespace Vtt.Server.Tests.Integration;

/// <summary>
/// Shares one <see cref="PostgresFixture"/> across every integration test class.
/// </summary>
/// <remarks>
/// xUnit v2 has no assembly-level fixture, so a collection is the widest scope available. Every
/// integration test class must join this collection rather than declaring its own fixture —
/// a fixture per class would start a container per class.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class IntegrationDatabase : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Integration";
}
