using ScrambleCoin.Domain.Enums;

namespace ScrambleCoin.Domain.Services;

/// <summary>
/// Calculates ranking points for a given game outcome.
/// </summary>
public static class RankingPointsCalculator
{
    /// <summary>
    /// Returns the number of ranking points awarded for the specified game result.
    /// Win = 3, Draw = 2, Loss = 1.
    /// </summary>
    public static int Calculate(GameResult result) => (int)result;
}
