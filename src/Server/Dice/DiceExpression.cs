using System.Globalization;
using System.Text.RegularExpressions;

namespace Vtt.Server.Dice;

/// <summary>Which dice to keep when an expression asks for fewer than it rolls.</summary>
public enum KeepRule
{
    All,
    Highest,
    Lowest,
}

/// <summary>
/// A parsed dice expression: how many dice, how many sides, what to keep, what to add.
/// </summary>
/// <remarks>
/// Bounded on purpose. <c>9999d9999</c> is a rejection rather than a request, because an
/// unbounded expression on a public endpoint is a denial of service with extra steps.
/// </remarks>
public sealed record DiceExpression(int Count, int Sides, KeepRule Keep, int KeepCount, int Modifier)
{
    public const int MaxCount = 100;

    public const int MaxSides = 1000;

    /// <summary>Smallest and largest total this expression can produce.</summary>
    public (int Minimum, int Maximum) Range()
    {
        var kept = Keep == KeepRule.All ? Count : KeepCount;

        return (kept + Modifier, (kept * Sides) + Modifier);
    }
}

public static partial class DiceParser
{
    /// <summary>
    /// Parses an expression, or returns null.
    /// </summary>
    /// <remarks>
    /// Null rather than an exception: a malformed expression is something a person typed, not an
    /// exceptional condition, and the caller turns it into a refusal.
    /// </remarks>
    public static DiceExpression? Parse(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        var match = Pattern().Match(expression.Trim().Replace(" ", string.Empty, StringComparison.Ordinal));

        if (!match.Success)
        {
            return null;
        }

        // "d20" means one d20.
        var count = match.Groups["count"].Success ? int.Parse(match.Groups["count"].Value, CultureInfo.InvariantCulture) : 1;
        var sides = int.Parse(match.Groups["sides"].Value, CultureInfo.InvariantCulture);

        if (count is < 1 or > DiceExpression.MaxCount || sides is < 2 or > DiceExpression.MaxSides)
        {
            return null;
        }

        var keep = KeepRule.All;
        var keepCount = count;

        if (match.Groups["keep"].Success)
        {
            keep = match.Groups["keep"].Value == "h" ? KeepRule.Highest : KeepRule.Lowest;
            keepCount = match.Groups["keepCount"].Success ? int.Parse(match.Groups["keepCount"].Value, CultureInfo.InvariantCulture) : 1;

            if (keepCount < 1 || keepCount > count)
            {
                return null;
            }
        }

        var modifier = 0;

        if (match.Groups["sign"].Success)
        {
            var magnitude = int.Parse(match.Groups["modifier"].Value, CultureInfo.InvariantCulture);
            modifier = match.Groups["sign"].Value == "-" ? -magnitude : magnitude;
        }

        return new DiceExpression(count, sides, keep, keepCount, modifier);
    }

    [GeneratedRegex(
        @"^(?<count>\d{1,3})?d(?<sides>\d{1,4})(k(?<keep>[hl])(?<keepCount>\d{1,3})?)?((?<sign>[+-])(?<modifier>\d{1,4}))?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
