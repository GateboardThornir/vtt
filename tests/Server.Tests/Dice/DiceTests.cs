using Microsoft.Extensions.DependencyInjection;
using Vtt.Server.Dice;

namespace Vtt.Server.Tests.Dice;

public class DiceParserTests
{
    [Theory]
    [InlineData("d20", 1, 20, 0)]
    [InlineData("2d6+3", 2, 6, 3)]
    [InlineData("4d6-1", 4, 6, -1)]
    [InlineData("1d100", 1, 100, 0)]
    [InlineData(" 2d8 + 2 ", 2, 8, 2)]
    [InlineData("D20", 1, 20, 0)]
    public void OrdinaryExpressionsParse(string expression, int count, int sides, int modifier)
    {
        var parsed = DiceParser.Parse(expression);

        Assert.NotNull(parsed);
        Assert.Equal(count, parsed.Count);
        Assert.Equal(sides, parsed.Sides);
        Assert.Equal(modifier, parsed.Modifier);
    }

    [Theory]
    [InlineData("2d20kh1", KeepRule.Highest, 1)]
    [InlineData("2d20kl1", KeepRule.Lowest, 1)]
    [InlineData("4d6kh3", KeepRule.Highest, 3)]
    [InlineData("2d20kh", KeepRule.Highest, 1)]
    public void KeepExpressionsParse(string expression, KeepRule keep, int keepCount)
    {
        var parsed = DiceParser.Parse(expression);

        Assert.NotNull(parsed);
        Assert.Equal(keep, parsed.Keep);
        Assert.Equal(keepCount, parsed.KeepCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("banana")]
    [InlineData("d")]
    [InlineData("0d6")]
    [InlineData("2d0")]
    [InlineData("2d1")]
    [InlineData("2d6+")]
    [InlineData("2d6++3")]
    [InlineData("2d20kh3")]
    [InlineData("d20; DROP TABLE users")]
    public void NonsenseIsRejected(string? expression) => Assert.Null(DiceParser.Parse(expression));

    [Theory]
    [InlineData("101d6")]
    [InlineData("2d1001")]
    [InlineData("999d999")]
    public void AbsurdExpressionsAreRejected(string expression)
    {
        // Unbounded expressions on a public endpoint are a denial of service with extra steps.
        Assert.Null(DiceParser.Parse(expression));
    }
}

public class DiceRollerTests
{
    private readonly IDiceRoller _roller = new ServiceCollection()
        .AddDice()
        .BuildServiceProvider()
        .GetRequiredService<IDiceRoller>();

    [Theory]
    [InlineData("d20")]
    [InlineData("2d6+3")]
    [InlineData("4d6-1")]
    [InlineData("3d8")]
    public void EveryResultLiesInsideItsPossibleRange(string expression)
    {
        var (minimum, maximum) = DiceParser.Parse(expression)!.Range();

        for (var attempt = 0; attempt < 300; attempt++)
        {
            var result = _roller.Roll(expression)!;

            Assert.InRange(result.Total, minimum, maximum);
        }
    }

    [Fact]
    public void TheFacesSumToTheTotal()
    {
        // The faces are what make a roll checkable rather than something the table has to accept.
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var result = _roller.Roll("3d6+2")!;

            Assert.Equal(3, result.Kept.Count);
            Assert.Equal(result.Kept.Sum() + result.Modifier, result.Total);
        }
    }

    [Fact]
    public void EveryFaceOfADSixEventuallyAppears()
    {
        // Catches a roller stuck on one value, which every other assertion here would pass.
        var seen = new HashSet<int>();

        for (var attempt = 0; attempt < 500; attempt++)
        {
            seen.Add(_roller.Roll("d6")!.Kept[0]);
        }

        Assert.Equal(6, seen.Count);
    }

    [Fact]
    public void KeepHighestKeepsTheHighestAndReportsTheRest()
    {
        for (var attempt = 0; attempt < 300; attempt++)
        {
            var result = _roller.Roll("2d20kh1")!;

            Assert.Single(result.Kept);
            Assert.Single(result.Dropped);

            // Advantage is more legible when you can see the die you did not use.
            Assert.True(result.Kept[0] >= result.Dropped[0]);
        }
    }

    [Fact]
    public void KeepLowestKeepsTheLowest()
    {
        for (var attempt = 0; attempt < 300; attempt++)
        {
            var result = _roller.Roll("2d20kl1")!;

            Assert.True(result.Kept[0] <= result.Dropped[0]);
        }
    }

    [Fact]
    public void FourDSixKeepThreeDropsExactlyOne()
    {
        var result = _roller.Roll("4d6kh3")!;

        Assert.Equal(3, result.Kept.Count);
        Assert.Single(result.Dropped);
        Assert.Equal(result.Kept.Sum(), result.Total);
    }

    [Fact]
    public void AnUnparseableExpressionRollsNothing() => Assert.Null(_roller.Roll("banana"));

    [Fact]
    public void TheExpressionIsEchoedBack()
    {
        // So a client can render what was asked for, not its own idea of it.
        Assert.Equal("2d6+3", _roller.Roll(" 2d6+3 ")!.Expression);
    }
}
