using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Services;
using ScrambleCoin.Domain.Enums;

namespace ScrambleCoin.Application.Games.MovePiece;

/// <summary>
/// Handles <see cref="MovePieceCommand"/>: loads the game, delegates movement to the domain,
/// and persists the updated state.
/// If the move causes the game to enter the <see cref="TurnPhase.CoinSpawn"/> phase (i.e. a new
/// turn has started), <see cref="CoinSpawnService"/> is invoked automatically so coins are placed
/// before control returns — bots never trigger coin spawning directly.
/// </summary>
public sealed class MovePieceCommandHandler : IRequestHandler<MovePieceCommand>
{
    private readonly IGameRepository _gameRepository;
    private readonly CoinSpawnService _coinSpawnService;
    private readonly ILogger<MovePieceCommandHandler> _logger;

    public MovePieceCommandHandler(
        IGameRepository gameRepository,
        CoinSpawnService coinSpawnService,
        ILogger<MovePieceCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _coinSpawnService = coinSpawnService;
        _logger = logger;
    }

    public async Task Handle(MovePieceCommand request, CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);

        var turnNumber = game.TurnNumber;

        game.MovePiece(request.PlayerId, request.PieceId, request.Segments);

        _logger.LogInformation(
            "Piece {PieceId} moved by player {PlayerId} in game {GameId} on turn {Turn}.",
            request.PieceId, request.PlayerId, request.GameId, turnNumber);

        // When both players have moved and the turn advances, the domain transitions to CoinSpawn.
        // Automatically execute coin spawning so the new turn is ready for PlacePhase.
        if (game.CurrentPhase == TurnPhase.CoinSpawn && game.Status == GameStatus.InProgress)
        {
            // CoinSpawnService saves the game after spawning + AdvancePhase.
            await _coinSpawnService.ExecuteForGameAsync(game, cancellationToken);
        }
        else
        {
            await _gameRepository.SaveAsync(game, cancellationToken);
        }
    }
}
