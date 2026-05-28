using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;

namespace ScrambleCoin.Application.Ranking.AwardRankingPoints;

/// <summary>
/// Handles <see cref="AwardRankingPointsCommand"/>:
/// <list type="bullet">
///   <item>Loads or creates a <see cref="RankingTrack"/> for each bot using their stable bot ID.</item>
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
        var botOneTrack = await LoadOrCreate(command.BotOneId, command.BotOneName, cancellationToken);
        var botTwoTrack = await LoadOrCreate(command.BotTwoId, command.BotTwoName, cancellationToken);

        if (command.IsDraw)
        {
            botOneTrack.RecordDraw();
            botTwoTrack.RecordDraw();

            _logger.LogInformation(
                "Game {GameId} (turn {TurnNumber}) ended in draw. " +
                "Awarded draw points to {BotOneId} and {BotTwoId}. " +
                "Bot1 now {Bot1Pts} pts, Bot2 now {Bot2Pts} pts.",
                command.GameId, command.TurnNumber,
                command.BotOneId, command.BotTwoId,
                botOneTrack.Points, botTwoTrack.Points);
        }
        else
        {
            // Determine winner vs loser by matching stable bot IDs
            var winnerTrack = command.WinnerId == command.BotOneId ? botOneTrack : botTwoTrack;
            var loserTrack  = command.WinnerId == command.BotOneId ? botTwoTrack : botOneTrack;

            winnerTrack.RecordWin();
            loserTrack.RecordLoss();

            _logger.LogInformation(
                "Game {GameId} (turn {TurnNumber}) ended. " +
                "Winner {WinnerId} now {WinPts} pts. Loser {LoserId} now {LosePts} pts.",
                command.GameId, command.TurnNumber,
                winnerTrack.BotId, winnerTrack.Points,
                loserTrack.BotId, loserTrack.Points);
        }

        await _rankingRepository.SaveAsync(botOneTrack, cancellationToken);
        await _rankingRepository.SaveAsync(botTwoTrack, cancellationToken);
    }

    private async Task<RankingTrack> LoadOrCreate(Guid botId, string botName, CancellationToken ct)
    {
        var track = await _rankingRepository.GetByBotIdAsync(botId, ct);
        return track ?? new RankingTrack(botId, botName);
    }
}
