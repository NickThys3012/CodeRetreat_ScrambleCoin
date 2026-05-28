namespace ScrambleCoin.Application.Games.GetGameResult;

/// <summary>
/// DTO returned by <see cref="GetGameResultQuery"/> when the game has finished.
/// </summary>
/// <param name="GameId">The unique identifier of the finished game.</param>
/// <param name="Status">Always <c>"finished"</c>.</param>
/// <param name="PlayerOneId">The player-slot identifier of the first player.</param>
/// <param name="PlayerOneScore">Final score for player one.</param>
/// <param name="PlayerTwoId">The player-slot identifier of the second player.</param>
/// <param name="PlayerTwoScore">Final score for player two.</param>
/// <param name="WinnerId">The player ID of the winner, or <c>null</c> if the game is a draw.</param>
/// <param name="IsDraw"><c>true</c> when both players have the same final score.</param>
public sealed record GetGameResultDto(
    Guid GameId,
    string Status,
    Guid PlayerOneId,
    int PlayerOneScore,
    Guid PlayerTwoId,
    int PlayerTwoScore,
    Guid? WinnerId,
    bool IsDraw);
