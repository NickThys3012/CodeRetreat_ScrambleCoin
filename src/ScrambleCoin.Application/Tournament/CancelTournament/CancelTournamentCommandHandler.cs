using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Enums;

namespace ScrambleCoin.Application.Tournament.CancelTournament;

/// <summary>
/// Handles <see cref="CancelTournamentCommand"/>: cancels the tournament and force-cancels
/// all active games that are still in progress or waiting for bots.
/// </summary>
public sealed class CancelTournamentCommandHandler : IRequestHandler<CancelTournamentCommand>
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly IGameRepository _gameRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CancelTournamentCommandHandler> _logger;

    public CancelTournamentCommandHandler(
        ITournamentRepository tournamentRepository,
        IGameRepository gameRepository,
        IUnitOfWork unitOfWork,
        ILogger<CancelTournamentCommandHandler> logger)
    {
        _tournamentRepository = tournamentRepository;
        _gameRepository = gameRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(CancelTournamentCommand request, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, cancellationToken);

        tournament.Cancel();

        // Collect all game IDs from incomplete group and knockout matches
        var activeGameIds = tournament.GroupMatches
            .Where(m => !m.IsCompleted && m.GameId.HasValue)
            .Select(m => m.GameId!.Value)
            .Concat(tournament.KnockoutMatches
                .Where(m => !m.IsCompleted && m.GameId.HasValue)
                .Select(m => m.GameId!.Value))
            .Distinct()
            .ToList();

        // Force-cancel each active game and stage it for persistence
        foreach (var gameId in activeGameIds)
        {
            Domain.Entities.Game game;
            try
            {
                game = await _gameRepository.GetByIdAsync(gameId, cancellationToken);
            }
            catch (Domain.Exceptions.GameNotFoundException)
            {
                _logger.LogWarning(
                    "Tournament {TournamentId}: game {GameId} referenced by a match was not found; skipping.",
                    request.TournamentId, gameId);
                continue;
            }

            if (game.Status is GameStatus.Finished or GameStatus.Cancelled)
                continue; // already terminal — nothing to do

            game.ForceCancel();
            await _gameRepository.StageAsync(game, cancellationToken);

            _logger.LogInformation(
                "Tournament {TournamentId}: game {GameId} force-cancelled.",
                request.TournamentId, gameId);
        }

        // Stage tournament and commit everything atomically
        await _tournamentRepository.SaveAsync(tournament, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Tournament {TournamentId} cancelled. {GameCount} active game(s) force-cancelled.",
            request.TournamentId, activeGameIds.Count);
    }
}
