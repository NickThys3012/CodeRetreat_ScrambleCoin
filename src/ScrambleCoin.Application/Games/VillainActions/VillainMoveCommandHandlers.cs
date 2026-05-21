using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Exceptions;

namespace ScrambleCoin.Application.Games.VillainActions;

/// <summary>
/// Handles <see cref="VillainMovePieceCommand"/>: moves a piece for the villain.
/// </summary>
public sealed class VillainMovePieceCommandHandler : IRequestHandler<VillainMovePieceCommand, VillainMoveResult>
{
    private readonly IGameRepository _gameRepository;
    private readonly ILogger<VillainMovePieceCommandHandler> _logger;

    public VillainMovePieceCommandHandler(IGameRepository gameRepository, ILogger<VillainMovePieceCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _logger = logger;
    }

    public async Task<VillainMoveResult> Handle(VillainMovePieceCommand request, CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);

        // Verify the villain ID matches the game's VillainId
        if (game.VillainId is null)
            throw new DomainException("This game does not have a villain.");

        // Verify it's the villain's turn (should be in MovePhase and villain is active player)
        if (game.MovePhaseActivePlayer != request.VillainPlayerId)
            throw new UnauthorizedGameAccessException();

        game.MovePiece(request.VillainPlayerId, request.PieceId, request.Segments);
        await _gameRepository.SaveAsync(game, cancellationToken);

        _logger.LogInformation(
            "Villain moved piece {PieceId} in game {GameId} during turn {Turn}",
            request.PieceId, request.GameId, game.TurnNumber);

        return new VillainMoveResult(game.CurrentPhase?.ToString(), game.MovePhaseActivePlayer?.ToString());
    }
}

/// <summary>
/// Handles <see cref="VillainSkipMovementCommand"/>: skips movement for the villain.
/// </summary>
public sealed class VillainSkipMovementCommandHandler : IRequestHandler<VillainSkipMovementCommand, VillainMoveResult>
{
    private readonly IGameRepository _gameRepository;
    private readonly ILogger<VillainSkipMovementCommandHandler> _logger;

    public VillainSkipMovementCommandHandler(IGameRepository gameRepository, ILogger<VillainSkipMovementCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _logger = logger;
    }

    public async Task<VillainMoveResult> Handle(VillainSkipMovementCommand request, CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);

        // Verify the villain ID matches the game's VillainId
        if (game.VillainId is null)
            throw new DomainException("This game does not have a villain.");

        // Verify it's the villain's turn (should be in MovePhase and villain is active player)
        if (game.MovePhaseActivePlayer != request.VillainPlayerId)
            throw new UnauthorizedGameAccessException();

        game.SkipMovement(request.VillainPlayerId);
        await _gameRepository.SaveAsync(game, cancellationToken);

        _logger.LogInformation(
            "Villain skipped movement in game {GameId} during turn {Turn}",
            request.GameId, game.TurnNumber);

        return new VillainMoveResult(game.CurrentPhase?.ToString(), game.MovePhaseActivePlayer?.ToString());
    }
}
