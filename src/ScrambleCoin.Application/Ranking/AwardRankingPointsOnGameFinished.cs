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
/// For tournament games the stable bot IDs are resolved from the tournament context;
/// for non-tournament PvP games the per-game slot IDs are used as a fallback.
/// </summary>
public sealed class AwardRankingPointsOnGameFinished : INotificationHandler<GameFinished>
{
    private readonly IGameRepository _gameRepository;
    private readonly ITournamentRepository _tournamentRepository;
    private readonly ISender _sender;
    private readonly ILogger<AwardRankingPointsOnGameFinished> _logger;

    public AwardRankingPointsOnGameFinished(
        IGameRepository gameRepository,
        ITournamentRepository tournamentRepository,
        ISender sender,
        ILogger<AwardRankingPointsOnGameFinished> logger)
    {
        _gameRepository = gameRepository;
        _tournamentRepository = tournamentRepository;
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

            // ── Resolve stable bot identities ─────────────────────────────────
            // game.PlayerOne / game.PlayerTwo are per-game slot GUIDs freshly minted when the game is
            // created — they are NOT stable across games. For tournament games we look up the stable
            // BotOne / BotTwo IDs from the tournament context. For non-tournament PvP games we fall
            // back to the slot IDs (acceptable: each game is a one-off and bots won't accumulate
            // correctly, but there is no better stable ID available outside of a tournament).

            Guid botOneId, botTwoId;
            string botOneName, botTwoName;
            Guid? stableWinnerId;

            var botInfo = await _tournamentRepository.GetBotInfoByGameIdAsync(
                notification.GameId, cancellationToken);

            if (botInfo is not null)
            {
                botOneId   = botInfo.BotOneId;
                botOneName = botInfo.BotOneName;
                botTwoId   = botInfo.BotTwoId;
                botTwoName = botInfo.BotTwoName;

                // Translate the player-slot WinnerId from GameFinished into the stable bot ID
                stableWinnerId = notification.WinnerId switch
                {
                    null                                                   => null,
                    var id when id == botInfo.BotOnePlayerId => botInfo.BotOneId,
                    var id when id == botInfo.BotTwoPlayerId => botInfo.BotTwoId,
                    var id                                                 => id  // unexpected; pass through
                };
            }
            else
            {
                // Non-tournament PvP fallback — slot IDs used as-is
                botOneId   = game.PlayerOne;
                botOneName = $"Bot-{game.PlayerOne:N}"[..13];
                botTwoId   = game.PlayerTwo;
                botTwoName = $"Bot-{game.PlayerTwo:N}"[..13];
                stableWinnerId = notification.WinnerId;
            }

            await _sender.Send(
                new AwardRankingPointsCommand(
                    GameId:     notification.GameId,
                    BotOneId:   botOneId,
                    BotOneName: botOneName,
                    BotTwoId:   botTwoId,
                    BotTwoName: botTwoName,
                    WinnerId:   stableWinnerId,
                    IsDraw:     notification.IsDraw,
                    TurnNumber: game.TurnNumber),
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
