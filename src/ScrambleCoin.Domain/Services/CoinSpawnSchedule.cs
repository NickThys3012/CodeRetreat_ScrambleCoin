using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;

namespace ScrambleCoin.Domain.Services;

/// <summary>
/// Defines the coin spawn schedule per turn number.
/// Turn 1: 7–9 silver initially + 2–4 additional silver (9–13 total).
/// Turns 2–3: 2–4 silver.
/// Turn 4: 4 gold (2 per player).
/// Turn 5: 3 gold total (2 for PlayerOne, 1 for PlayerTwo — placed on random free tiles, no zone distinction).
/// </summary>
public static class CoinSpawnSchedule
{
    /// <summary>
    /// Returns the ordered list of <see cref="CoinType"/> values to spawn for <paramref name="turnNumber"/>.
    /// </summary>
    /// <exception cref="DomainException">Thrown for any turn number outside [1, 5].</exception>
    public static IReadOnlyList<CoinType> For(int turnNumber, Random random)
    {
        return turnNumber switch
        {
            1 => Repeat(CoinType.Silver, random.Next(7, 10) + random.Next(2, 5)),
            2 or 3 => Repeat(CoinType.Silver, random.Next(2, 5)),
            4 => Repeat(CoinType.Gold, 4),
            5 => Repeat(CoinType.Gold, 3),
            _ => throw new DomainException($"No coin spawn schedule defined for turn {turnNumber}. Valid turns are 1–5.")
        };
    }

    private static IReadOnlyList<CoinType> Repeat(CoinType coinType, int count) =>
        Enumerable.Repeat(coinType, count).ToList().AsReadOnly();
}
