using System.Buffers.Text;
using Vtt.Server.Accounts;

namespace Vtt.Server.Tests.Accounts;

public class SecureTokenTests
{
    [Fact]
    public void EveryTokenIsDifferent()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => SecureToken.Generate()).ToList();

        Assert.Equal(tokens.Count, tokens.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ATokenCarriesTheFullTwoHundredAndFiftySixBits()
    {
        var token = SecureToken.Generate();

        Assert.Equal(SecureToken.TokenBytes, Base64Url.DecodeFromChars(token).Length);
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
            Assert.DoesNotContain(forbidden, SecureToken.Generate());
        }
    }

    [Fact]
    public void HashingIsRepeatable()
    {
        // Redemption finds the row by hashing the presented token and looking it up, so the same
        // token must always produce the same hash.
        var token = SecureToken.Generate();

        Assert.Equal(SecureToken.Hash(token), SecureToken.Hash(token));
    }

    [Fact]
    public void DifferentTokensHashDifferently()
    {
        Assert.NotEqual(SecureToken.Hash(SecureToken.Generate()), SecureToken.Hash(SecureToken.Generate()));
    }

    [Fact]
    public void TheHashDoesNotContainTheToken()
    {
        var token = SecureToken.Generate();

        Assert.DoesNotContain(token, SecureToken.Hash(token), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheHashIsTheDeclaredLength()
    {
        // The column is sized to this, so a mismatch would be a truncation rather than an error.
        Assert.Equal(SecureToken.HashLength, SecureToken.Hash(SecureToken.Generate()).Length);
    }
}
