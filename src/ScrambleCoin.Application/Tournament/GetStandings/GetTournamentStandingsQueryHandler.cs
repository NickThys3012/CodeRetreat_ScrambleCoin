using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Entities;
using DomainTournament = ScrambleCoin.Domain.Tournaments.Tournament;

namespace ScrambleCoin.Application.Tournament.GetStandings;

/// <summary>
/// Handles <see cref="GetTournamentStandingsQuery"/>:
/// lazily syncs game results, potentially advances the tournament to the knockout stage,
/// and returns the current standings table.
/// </summary>
public sealed class GetTournamentStandingsQueryHandler : IRequestHandler<GetTournamentStandingsQuery, TournamentStandingsDto>
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly IGameRepository _gameRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetTournamentStandingsQueryHandler> _logger;

    public GetTournamentStandingsQueryHandler(
        ITournamentRepository tournamentRepository,
        IGameRepository gameRepository,
        IUnitOfWork unitOfWork,
        ILogger<GetTournamentStandingsQueryHandler> logger)
    {
        _tournamentRepository = tournamentRepository;
        _gameRepository = gameRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<TournamentStandingsDto> Handle(GetTournamentStandingsQuery request, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, cancellationToken);

        // Sync game results if the tournament is in GroupStage
        var dirty = false;
        if (tournament.Status == TournamentStatus.GroupStage)
        {
            dirty = await SyncGroupResultsAsync(tournament, cancellationToken);
        }

        if (dirty)
        {
            await _tournamentRepository.SaveAsync(tournament, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var standings = tournament.ComputeStandings();

        var entries = standings.Select((s, index) => new StandingEntryDto(
            Rank: index + 1,
            BotId: s.BotId,
            BotName: s.BotName,
            Played: s.Wins + s.Draws + s.Losses,
            Wins: s.Wins,
            Draws: s.Draws,
            Losses: s.Losses,
            Points: s.Points,
            TotalCoins: s.TotalCoins)).ToList();

        return new TournamentStandingsDto(
            tournament.Id,
            tournament.Name,
            tournament.Status.ToString(),
            entries.AsReadOnly());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// For each incomplete group match that has an assigned game, checks if that game
    /// has finished and records the result. Returns true if any match was updated.
    /// </summary>
    private async Task<bool> SyncGroupResultsAsync(DomainTournament tournament, CancellationToken cancellationToken)
    {
        var dirty = false;

        foreach (var match in tournament.GroupMatches.Where(m => m is { IsCompleted: false, GameId: not null }))
        {
            Game game;
            try
            {
                game = await _gameRepository.GetByIdAsync(match.GameId!.Value, cancellationToken);
            }
            catch (Domain.Exceptions.GameNotFoundException)
            {
                continue;
            }

            if (game.Status != GameStatus.Finished)
                continue;

            game.Scores.TryGetValue(game.PlayerOne, out var scoreOne);
            game.Scores.TryGetValue(game.PlayerTwo, out var scoreTwo);

            var isDraw = scoreOne == scoreTwo;
            Guid? gameWinnerId = isDraw ? null : (scoreOne > scoreTwo ? game.PlayerOne : game.PlayerTwo);

            // Map game player IDs to bot scores using the match token mapping
            var botOneScore = match.BotOnePlayerId == game.PlayerOne ? scoreOne : scoreTwo;
            var botTwoScore = match.BotTwoPlayerId == game.PlayerTwo ? scoreTwo : scoreOne;

            tournament.RecordGroupResult(match.Id, gameWinnerId, isDraw, botOneScore, botTwoScore);

            _logger.LogInformation(
                "Tournament {TournamentId}: group match {MatchId} result synced. Winner={Winner} BotOneScore={BotOneScore} BotTwoScore={BotTwoScore}",
                tournament.Id, match.Id, gameWinnerId?.ToString() ?? "draw", botOneScore, botTwoScore);

            dirty = true;
        }

        return dirty;
    }
}
