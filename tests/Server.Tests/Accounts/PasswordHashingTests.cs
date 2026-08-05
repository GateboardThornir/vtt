using Microsoft.Extensions.DependencyInjection;
using Vtt.Server.Accounts;

namespace Vtt.Server.Tests.Accounts;

public class PasswordHashingTests
{
    // Resolved through the module's own registration rather than constructed directly. The
    // implementation is internal and stays that way — InternalsVisibleTo would open every internal
    // of the server to the tests — and going through AddAccounts also proves it is wired up.
    private readonly IPasswordHasher _hasher = new ServiceCollection()
        .AddAccounts()
        .BuildServiceProvider()
        .GetRequiredService<IPasswordHasher>();

    [Fact]
    public void HashDoesNotContainThePassword()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.DoesNotContain("correct", hash, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("staple", hash, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheSamePasswordHashesDifferentlyEveryTime()
    {
        // A per-password salt is what makes precomputed tables useless. Two identical passwords
        // producing identical hashes would mean there is no salt at all.
        var first = _hasher.Hash("same password");
        var second = _hasher.Hash("same password");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void VerifySucceedsForTheCorrectPassword()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.Equal(PasswordVerification.Success, _hasher.Verify("correct horse battery staple", hash));
    }

    [Fact]
    public void VerifyFailsForTheWrongPassword()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.Equal(PasswordVerification.Failed, _hasher.Verify("Correct horse battery staple", hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a hash at all")]
    [InlineData("AAAAAA")]
    public void VerifyFailsRatherThanThrowsOnAnUnreadableHash(string hash)
    {
        // A corrupt row must not become an exception during login: a 500 for one account and a
        // clean rejection for every other is itself a disclosure about that account.
        Assert.Equal(PasswordVerification.Failed, _hasher.Verify("any password", hash));
    }

    [Fact]
    public void HashingAndVerifyingDoNotNeedAUserInstance()
    {
        // The framework's PasswordHasher<TUser> is generic only so callers can implement a
        // per-user rehash policy; the built-in implementation never dereferences the instance, so
        // the wrapper passes null. This test is what stops that being an assumption.
        var hash = _hasher.Hash("no user object anywhere");

        Assert.Equal(PasswordVerification.Success, _hasher.Verify("no user object anywhere", hash));
    }
}
