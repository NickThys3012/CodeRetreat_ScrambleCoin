using MediatR;

namespace ScrambleCoin.Application.Ranking.AwardRankingPoints;

/// <summary>
/// Awards ranking points to both players after a game ends.
/// </summary>
/// <param name="GameId">The ID of the finished game.</param>
/// <param name="PlayerOneId">Player-slot ID of player one.</param>
/// <param name="PlayerTwoId">Player-slot ID of player two.</param>
/// <param name="WinnerId">The winning player's slot ID, or <c>null</c> for a draw.</param>
/// <param name="IsDraw">True when the game ended in a draw.</param>
public sealed record AwardRankingPointsCommand(
    Guid GameId,
    Guid PlayerOneId,
    Guid PlayerTwoId,
    Guid? WinnerId,
    bool IsDraw) : IRequest;
