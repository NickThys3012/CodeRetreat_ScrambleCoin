namespace ScrambleCoin.Domain.Tournaments;

/// <summary>
/// Immutable snapshot of a bot's group stage standing in a tournament.
/// </summary>
/// <param name="BotId">The bot's tournament identity.</param>
/// <param name="BotName">Human-readable bot name.</param>
/// <param name="Wins">Number of group games won (3 points each).</param>
/// <param name="Draws">Number of group games drawn (2 points each).</param>
/// <param name="Losses">Number of group games lost (1 point each).</param>
/// <param name="Points">Total points accumulated (3W + 2D + 1L).</param>
/// <param name="TotalCoins">Total coin score across all completed group games (tie-break).</param>
public sealed record TournamentStanding(
    Guid BotId,
    string BotName,
    int Wins,
    int Draws,
    int Losses,
    int Points,
    int TotalCoins);
