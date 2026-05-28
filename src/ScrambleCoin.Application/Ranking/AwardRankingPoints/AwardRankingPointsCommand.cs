using MediatR;

namespace ScrambleCoin.Application.Ranking.AwardRankingPoints;

/// <summary>
/// Awards ranking points to both players after a game ends.
/// </summary>
/// <param name="GameId">The ID of the finished game.</param>
/// <param name="BotOneId">Stable bot ID of the first participant.</param>
/// <param name="BotOneName">Display name of the first participant (used when creating a new <c>RankingTrack</c>).</param>
/// <param name="BotTwoId">Stable bot ID of the second participant.</param>
/// <param name="BotTwoName">Display name of the second participant (used when creating a new <c>RankingTrack</c>).</param>
/// <param name="WinnerId">The stable bot ID of the winner, or <c>null</c> for a draw.</param>
/// <param name="IsDraw">True when the game ended in a draw.</param>
/// <param name="TurnNumber">The turn number at which the game ended (used for structured logging).</param>
public sealed record AwardRankingPointsCommand(
    Guid GameId,
    Guid BotOneId,
    string BotOneName,
    Guid BotTwoId,
    string BotTwoName,
    Guid? WinnerId,
    bool IsDraw,
    int TurnNumber) : IRequest;
