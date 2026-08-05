using Microsoft.Extensions.Configuration;
using Vtt.Server.Infrastructure;

namespace Vtt.Server.Tests.Infrastructure;

public class DatabaseConnectionStringTests
{
    [Fact]
    public void ResolveReturnsTheConfiguredConnectionString()
    {
        var configuration = Build(("ConnectionStrings:Default", "Host=localhost;Database=vtt"));

        Assert.Equal("Host=localhost;Database=vtt", DatabaseConnectionString.Resolve(configuration));
    }

    [Fact]
    public void ResolveThrowsWhenNoConnectionStringIsConfigured()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => DatabaseConnectionString.Resolve(Build()));

        Assert.Contains("ConnectionStrings__Default", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveThrowsWhenTheConnectionStringIsBlank()
    {
        var configuration = Build(("ConnectionStrings:Default", "   "));

        Assert.Throws<InvalidOperationException>(() => DatabaseConnectionString.Resolve(configuration));
    }

    [Fact]
    public void RedactReplacesThePasswordValue()
    {
        var redacted = DatabaseConnectionString.Redact(
            "Host=localhost;Port=55432;Database=vtt;Username=vtt;Password=hunter2");

        Assert.DoesNotContain("hunter2", redacted, StringComparison.Ordinal);
        Assert.Equal("Host=localhost;Port=55432;Database=vtt;Username=vtt;Password=***", redacted);
    }

    [Theory]
    [InlineData("password=hunter2")]
    [InlineData("PASSWORD=hunter2")]
    [InlineData(" Password =hunter2")]
    [InlineData("Pwd=hunter2")]
    public void RedactRecognisesEverySpellingOfThePasswordKey(string segment)
    {
        var redacted = DatabaseConnectionString.Redact($"Host=localhost;{segment}");

        Assert.DoesNotContain("hunter2", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void RedactKeepsEverythingThatIsNotThePassword()
    {
        var redacted = DatabaseConnectionString.Redact("Host=db.example;Port=5432;Database=vtt");

        Assert.Equal("Host=db.example;Port=5432;Database=vtt", redacted);
    }

    private static IConfiguration Build(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value))
            .Build();
}
