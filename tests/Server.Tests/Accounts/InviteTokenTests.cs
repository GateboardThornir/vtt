using System.Buffers.Text;
using Vtt.Server.Accounts;

namespace Vtt.Server.Tests.Accounts;

public class InviteTokenTests
{
    [Fact]
    public void EveryTokenIsDifferent()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => InviteToken.Generate()).ToList();

        Assert.Equal(tokens.Count, tokens.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ATokenCarriesTheFullTwoHundredAndFiftySixBits()
    {
        var token = InviteToken.Generate();

        Assert.Equal(InviteToken.TokenBytes, Base64Url.DecodeFromChars(token).Length);
    }

    [Theory]
    [InlineData('+')]
    [InlineData('/')]
    [InlineData('=')]
    public void ATokenContainsNothingThatWouldNeedEscapingInAUrl(char forbidden)
    {
        // The token travels in an invitation URL. Standard base64 uses all three of these, and each
        // means something else in a URL.
        for (var attempt = 0; attempt < 200; attempt++)
        {
            Assert.DoesNotContain(forbidden, InviteToken.Generate());
        }
    }

    [Fact]
    public void HashingIsRepeatable()
    {
        // Redemption finds the row by hashing the presented token and looking it up, so the same
        // token must always produce the same hash.
        var token = InviteToken.Generate();

        Assert.Equal(InviteToken.Hash(token), InviteToken.Hash(token));
    }

    [Fact]
    public void DifferentTokensHashDifferently()
    {
        Assert.NotEqual(InviteToken.Hash(InviteToken.Generate()), InviteToken.Hash(InviteToken.Generate()));
    }

    [Fact]
    public void TheHashDoesNotContainTheToken()
    {
        var token = InviteToken.Generate();

        Assert.DoesNotContain(token, InviteToken.Hash(token), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheHashIsTheDeclaredLength()
    {
        // The column is sized to this, so a mismatch would be a truncation rather than an error.
        Assert.Equal(InviteToken.HashLength, InviteToken.Hash(InviteToken.Generate()).Length);
    }
}
