using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;

namespace ScrambleCoin.Application.Ranking.AwardRankingPoints;

/// <summary>
/// Handles <see cref="AwardRankingPointsCommand"/>:
/// <list type="bullet">
///   <item>Loads or creates a <see cref="RankingTrack"/> for each player.</item>
///   <item>Calls <c>RecordWin</c>, <c>RecordDraw</c>, or <c>RecordLoss</c> on each track.</item>
///   <item>Persists both tracks via <see cref="IRankingRepository"/>.</item>
/// </list>
/// </summary>
public sealed class AwardRankingPointsCommandHandler : IRequestHandler<AwardRankingPointsCommand>
{
    private readonly IRankingRepository _rankingRepository;
    private readonly ILogger<AwardRankingPointsCommandHandler> _logger;

    public AwardRankingPointsCommandHandler(
        IRankingRepository rankingRepository,
        ILogger<AwardRankingPointsCommandHandler> logger)
    {
        _rankingRepository = rankingRepository;
        _logger = logger;
    }

    public async Task Handle(AwardRankingPointsCommand command, CancellationToken cancellationToken)
    {
        var playerOneTrack = await LoadOrCreate(command.PlayerOneId, cancellationToken);
        var playerTwoTrack = await LoadOrCreate(command.PlayerTwoId, cancellationToken);

        if (command.IsDraw)
        {
            playerOneTrack.RecordDraw();
            playerTwoTrack.RecordDraw();

            _logger.LogInformation(
                "Game {GameId} ended in draw. Awarded draw points to {P1} and {P2}. " +
                "P1 now {P1Pts} pts, P2 now {P2Pts} pts.",
                command.GameId,
                command.PlayerOneId, command.PlayerTwoId,
                playerOneTrack.Points, playerTwoTrack.Points);
        }
        else
        {
            // Determine winner vs loser
            var winnerTrack = command.WinnerId == command.PlayerOneId ? playerOneTrack : playerTwoTrack;
            var loserTrack  = command.WinnerId == command.PlayerOneId ? playerTwoTrack : playerOneTrack;

            winnerTrack.RecordWin();
            loserTrack.RecordLoss();

            _logger.LogInformation(
                "Game {GameId} ended. Winner {WinnerId} now {WinPts} pts. " +
                "Loser {LoserId} now {LosePts} pts.",
                command.GameId,
                winnerTrack.BotId, winnerTrack.Points,
                loserTrack.BotId, loserTrack.Points);
        }

        await _rankingRepository.SaveAsync(playerOneTrack, cancellationToken);
        await _rankingRepository.SaveAsync(playerTwoTrack, cancellationToken);
    }

    private async Task<RankingTrack> LoadOrCreate(Guid botId, CancellationToken ct)
    {
        var track = await _rankingRepository.GetByBotIdAsync(botId, ct);
        if (track is not null)
            return track;

        // No existing track — create a new one with a generated display name
        var botName = $"Bot-{botId.ToString("N")[..8]}";
        return new RankingTrack(botId, botName);
    }
}
