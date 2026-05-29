namespace ScrambleCoin.Application.Games.Admin;

/// <summary>
/// Lightweight summary of a game for the admin monitoring panel.
/// Returned by <see cref="GetAllActiveGamesQuery"/> without loading the full Game aggregate.
/// </summary>
/// <param name="GameId">Unique identifier of the game.</param>
/// <param name="PlayerOne">Player-slot identifier of the first player.</param>
/// <param name="PlayerTwo">Player-slot identifier of the second player.</param>
/// <param name="Status">Human-readable game status string (e.g. "InProgress", "WaitingForBots").</param>
/// <param name="TurnNumber">Current turn number (0 before the game starts).</param>
/// <param name="Phase">Current phase name, or <c>null</c> when no phase is active.</param>
/// <param name="ScorePlayerOne">Coin score for <see cref="PlayerOne"/>.</param>
/// <param name="ScorePlayerTwo">Coin score for <see cref="PlayerTwo"/>.</param>
public sealed record ActiveGameSummaryDto(
    Guid GameId,
    Guid PlayerOne,
    Guid PlayerTwo,
    string Status,
    int TurnNumber,
    string? Phase,
    int ScorePlayerOne,
    int ScorePlayerTwo);
