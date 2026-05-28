using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Notifications;
using ScrambleCoin.Application.Ranking.AwardRankingPoints;
using ScrambleCoin.Application.Interfaces;

namespace ScrambleCoin.Application.Ranking;

/// <summary>
/// Reacts to a <see cref="GameFinished"/> notification by dispatching
/// <see cref="AwardRankingPointsCommand"/> to update both players' ranking tracks.
/// Solo-mode games (where PlayerTwo is a villain CPU) are excluded.
/// </summary>
public sealed class AwardRankingPointsOnGameFinished : INotificationHandler<GameFinished>
{
    private readonly IGameRepository _gameRepository;
    private readonly ISender _sender;
    private readonly ILogger<AwardRankingPointsOnGameFinished> _logger;

    public AwardRankingPointsOnGameFinished(
        IGameRepository gameRepository,
        ISender sender,
        ILogger<AwardRankingPointsOnGameFinished> logger)
    {
        _gameRepository = gameRepository;
        _sender = sender;
        _logger = logger;
    }

    public async Task Handle(GameFinished notification, CancellationToken cancellationToken)
    {
        try
        {
            var game = await _gameRepository.GetByIdAsync(notification.GameId, cancellationToken);

            // Solo mode games don't count toward the PvP ranking leaderboard
            if (game.GameMode == Domain.Enums.GameMode.Solo)
            {
                _logger.LogDebug(
                    "Game {GameId} is a solo-mode game — skipping ranking point award.",
                    notification.GameId);
                return;
            }

            await _sender.Send(
                new AwardRankingPointsCommand(
                    GameId:      notification.GameId,
                    PlayerOneId: game.PlayerOne,
                    PlayerTwoId: game.PlayerTwo,
                    WinnerId:    notification.WinnerId,
                    IsDraw:      notification.IsDraw),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error awarding ranking points for game {GameId}. Rankings not updated.",
                notification.GameId);
            // Do not re-throw — the game itself is already finished; ranking is best-effort.
        }
    }
}
