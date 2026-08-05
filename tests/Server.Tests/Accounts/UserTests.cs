using Vtt.Server.Accounts;

namespace Vtt.Server.Tests.Accounts;

public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RegisterStartsThePersonInPending()
    {
        var user = User.Register("Mattia", "hash", Now);

        Assert.Equal(AccountState.Pending, user.State);
    }

    [Fact]
    public void RegisterKeepsTheUsernameAsTypedForDisplay()
    {
        var user = User.Register("Mattia", "hash", Now);

        Assert.Equal("Mattia", user.Username);
    }

    [Fact]
    public void RegisterNormalisesTheUsernameForTheUniqueIndex()
    {
        var user = User.Register("Mattia", "hash", Now);

        Assert.Equal("mattia", user.UsernameNormalized);
    }

    [Theory]
    [InlineData("Mattia", "mattia")]
    [InlineData("MATTIA", "mattia")]
    [InlineData("mAtTiA", "mattia")]
    public void UsernamesDifferingOnlyInCaseNormaliseIdentically(string typed, string expected)
    {
        Assert.Equal(expected, User.Normalize(typed));
    }

    [Fact]
    public void RegisterAssignsATimeOrderedIdentifier()
    {
        // Version 7 keeps inserts at the end of the primary key index instead of scattering them.
        var user = User.Register("Mattia", "hash", Now);

        Assert.Equal(7, user.Id.Version);
    }

    [Fact]
    public void RegisterRecordsTheSuppliedTime()
    {
        var user = User.Register("Mattia", "hash", Now);

        Assert.Equal(Now, user.CreatedAt);
    }
}
