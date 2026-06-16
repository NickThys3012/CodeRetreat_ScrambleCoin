namespace ScrambleCoin.Domain.Enums;

/// <summary>
/// Represents the outcome of a game from a single bot's perspective.
/// The integer value is the number of ranking points awarded.
/// </summary>
 #pragma warning disable CA1008
public enum GameResult
 #pragma warning restore CA1008
{
    Win  = 3,
    Draw = 2,
    Loss = 1
}
