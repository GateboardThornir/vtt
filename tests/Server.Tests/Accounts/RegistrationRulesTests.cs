using Vtt.Server.Accounts;

namespace Vtt.Server.Tests.Accounts;

public class RegistrationRulesTests
{
    [Theory]
    [InlineData("mattia")]
    [InlineData("Mattia")]
    [InlineData("a-b_c")]
    [InlineData("player1")]
    [InlineData("ABC")]
    public void OrdinaryUsernamesAreAccepted(string username) =>
        Assert.True(RegistrationRules.IsWellFormedUsername(username));

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("with space")]
    [InlineData("with.dot")]
    [InlineData("with@sign")]
    [InlineData("with/slash")]
    [InlineData(null)]
    public void MalformedUsernamesAreRejected(string? username) =>
        Assert.False(RegistrationRules.IsWellFormedUsername(username));

    [Theory]
    [InlineData("mattià")]
    [InlineData("mаttia")]
    [InlineData("日本語")]
    public void NonAsciiUsernamesAreRejected(string username)
    {
        // The second case is the reason this rule exists: it is "mattia" with a Cyrillic 'а'. It
        // looks identical to the Latin spelling and is a different string, so allowing it would let
        // one person register a name indistinguishable from another's.
        Assert.False(RegistrationRules.IsWellFormedUsername(username));
    }

    [Fact]
    public void UsernameLengthBoundsAreInclusive()
    {
        Assert.True(RegistrationRules.IsWellFormedUsername(new string('a', RegistrationRules.UsernameMinLength)));
        Assert.True(RegistrationRules.IsWellFormedUsername(new string('a', RegistrationRules.UsernameMaxLength)));
        Assert.False(RegistrationRules.IsWellFormedUsername(new string('a', RegistrationRules.UsernameMinLength - 1)));
        Assert.False(RegistrationRules.IsWellFormedUsername(new string('a', RegistrationRules.UsernameMaxLength + 1)));
    }

    [Fact]
    public void AUsernameCannotOutgrowItsColumn() =>
        Assert.True(RegistrationRules.UsernameMaxLength <= User.UsernameMaxLength);

    [Fact]
    public void PasswordLengthIsTheOnlyRule()
    {
        var longEnough = new string('x', RegistrationRules.PasswordMinLength);

        // No symbol, no digit, no capital — and accepted, deliberately.
        Assert.True(RegistrationRules.IsAcceptablePassword(longEnough));
        Assert.False(RegistrationRules.IsAcceptablePassword(longEnough[1..]));
        Assert.False(RegistrationRules.IsAcceptablePassword(null));
    }

    [Fact]
    public void AShortButComplexPasswordIsStillRejected() =>
        Assert.False(RegistrationRules.IsAcceptablePassword("Aa1!Aa1!"));
}
