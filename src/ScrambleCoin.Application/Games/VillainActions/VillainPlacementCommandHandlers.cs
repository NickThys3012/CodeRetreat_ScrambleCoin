using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Games.VillainActions;

/// <summary>
/// Handles <see cref="VillainPlacePieceCommand"/>: places a piece for the villain.
/// </summary>
public sealed class VillainPlacePieceCommandHandler : IRequestHandler<VillainPlacePieceCommand, VillainPlacementResult>
{
    private readonly IGameRepository _gameRepository;
    private readonly ILogger<VillainPlacePieceCommandHandler> _logger;

    public VillainPlacePieceCommandHandler(IGameRepository gameRepository, ILogger<VillainPlacePieceCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _logger = logger;
    }

    public async Task<VillainPlacementResult> Handle(VillainPlacePieceCommand request, CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);

        // Verify the villain ID matches the game's VillainId
        if (game.VillainId is null)
            throw new DomainException("This game does not have a villain.");

        // Verify it's the villain's turn (should be PlayerTwo)
        if (request.VillainPlayerId != game.PlayerTwo)
            throw new UnauthorizedGameAccessException();

        game.PlacePiece(request.VillainPlayerId, request.PieceId, request.Position);
        await _gameRepository.SaveAsync(game, cancellationToken);

        _logger.LogInformation(
            "Villain placed piece {PieceId} at {Position} in game {GameId} on turn {Turn}",
            request.PieceId, request.Position, request.GameId, game.TurnNumber);

        return new VillainPlacementResult(game.CurrentPhase?.ToString(), game.MovePhaseActivePlayer?.ToString());
    }
}

/// <summary>
/// Handles <see cref="VillainSkipPlacementCommand"/>: skips placement for the villain.
/// </summary>
public sealed class VillainSkipPlacementCommandHandler : IRequestHandler<VillainSkipPlacementCommand, VillainPlacementResult>
{
    private readonly IGameRepository _gameRepository;
    private readonly ILogger<VillainSkipPlacementCommandHandler> _logger;

    public VillainSkipPlacementCommandHandler(IGameRepository gameRepository, ILogger<VillainSkipPlacementCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _logger = logger;
    }

    public async Task<VillainPlacementResult> Handle(VillainSkipPlacementCommand request, CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);

        // Verify the villain ID matches the game's VillainId
        if (game.VillainId is null)
            throw new DomainException("This game does not have a villain.");

        // Verify it's the villain's turn (should be PlayerTwo)
        if (request.VillainPlayerId != game.PlayerTwo)
            throw new UnauthorizedGameAccessException();

        game.SkipPlacement(request.VillainPlayerId);
        await _gameRepository.SaveAsync(game, cancellationToken);

        _logger.LogInformation(
            "Villain skipped placement in game {GameId} on turn {Turn}",
            request.GameId, game.TurnNumber);

        return new VillainPlacementResult(game.CurrentPhase?.ToString(), game.MovePhaseActivePlayer?.ToString());
    }
}
