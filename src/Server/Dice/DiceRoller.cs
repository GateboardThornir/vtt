using System.Security.Cryptography;

namespace Vtt.Server.Dice;

/// <summary>
/// One roll, in enough detail to be shown rather than merely asserted.
/// </summary>
/// <remarks>
/// Every die face is reported. A total on its own cannot be displayed convincingly and cannot be
/// checked by anybody; the faces are what let a client render <c>[4, 6] + 3 = 13</c> instead of
/// asking the table to take its word for it.
/// </remarks>
public sealed record RollResult(
    string Expression,
    IReadOnlyList<int> Kept,
    IReadOnlyList<int> Dropped,
    int Modifier,
    int Total);

public interface IDiceRoller
{
    /// <summary>Rolls an expression, or returns null if it does not parse.</summary>
    RollResult? Roll(string expression);
}

internal sealed class DiceRoller : IDiceRoller
{
    public RollResult? Roll(string expression)
    {
        var parsed = DiceParser.Parse(expression);

        if (parsed is null)
        {
            return null;
        }

        var faces = new int[parsed.Count];

        for (var index = 0; index < faces.Length; index++)
        {
            // RandomNumberGenerator rather than Random, and GetInt32 rather than `% sides`, which
            // is biased toward the low faces whenever the range does not divide evenly. Not because
            // somebody will predict a seed, but because there is no reason to accept a weaker
            // source for the one number the whole game hangs on — and here it costs nothing.
            faces[index] = RandomNumberGenerator.GetInt32(1, parsed.Sides + 1);
        }

        var (kept, dropped) = Select(faces, parsed);

        return new RollResult(
            expression.Trim(),
            kept,
            dropped,
            parsed.Modifier,
            kept.Sum() + parsed.Modifier);
    }

    /// <remarks>
    /// The dropped dice are reported too: advantage is more legible when you can see the die you
    /// did not use.
    /// </remarks>
    private static (IReadOnlyList<int> Kept, IReadOnlyList<int> Dropped) Select(
        int[] faces,
        DiceExpression expression)
    {
        if (expression.Keep == KeepRule.All)
        {
            return (faces, []);
        }

        var ordered = expression.Keep == KeepRule.Highest
            ? faces.OrderByDescending(face => face).ToArray()
            : faces.OrderBy(face => face).ToArray();

        return (ordered[..expression.KeepCount], ordered[expression.KeepCount..]);
    }
}

public static class DiceServices
{
    public static IServiceCollection AddDice(this IServiceCollection services) =>
        // Stateless: the randomness comes from a static cryptographic source, not from held state.
        services.AddSingleton<IDiceRoller, DiceRoller>();
}
