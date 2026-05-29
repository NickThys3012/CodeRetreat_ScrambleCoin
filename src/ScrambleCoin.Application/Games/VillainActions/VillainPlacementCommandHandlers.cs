using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Notifications;
using ScrambleCoin.Domain.Events;
using ScrambleCoin.Domain.Exceptions;

namespace ScrambleCoin.Application.Games.VillainActions;

/// <summary>
/// Handles <see cref="VillainPlacePieceCommand"/>: places a piece for the villain.
/// </summary>
public sealed class VillainPlacePieceCommandHandler : IRequestHandler<VillainPlacePieceCommand, VillainPlacementResult>
{
    private readonly IGameRepository _gameRepository;
    private readonly IPublisher _publisher;
    private readonly ILogger<VillainPlacePieceCommandHandler> _logger;

    public VillainPlacePieceCommandHandler(
        IGameRepository gameRepository,
        IPublisher publisher,
        ILogger<VillainPlacePieceCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<VillainPlacementResult> Handle(VillainPlacePieceCommand request, CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);

        if (game.VillainId is null)
            throw new DomainException("This game does not have a villain.");

        if (request.VillainPlayerId != game.PlayerTwo)
            throw new UnauthorizedGameAccessException();

        game.PlacePiece(request.VillainPlayerId, request.PieceId, request.Position);

        // Capture events BEFORE SaveAsync clears them.
        var phaseEvents = game.DomainEvents.OfType<TurnPhaseAdvanced>().ToList();

        await _gameRepository.SaveAsync(game, cancellationToken);

        _logger.LogInformation(
            "Villain placed piece {PieceId} at {Position} in game {GameId} on turn {Turn}",
            request.PieceId, request.Position, request.GameId, game.TurnNumber);

        foreach (var e in phaseEvents)
            await _publisher.Publish(
                new TurnPhaseChangedNotification(e.GameId, e.TurnNumber, e.PreviousPhase.ToString(), e.NewPhase?.ToString()),
                cancellationToken);

        return new VillainPlacementResult(game.CurrentPhase?.ToString(), game.MovePhaseActivePlayer?.ToString());
    }
}

/// <summary>
/// Handles <see cref="VillainSkipPlacementCommand"/>: skips placement for the villain.
/// </summary>
public sealed class VillainSkipPlacementCommandHandler : IRequestHandler<VillainSkipPlacementCommand, VillainPlacementResult>
{
    private readonly IGameRepository _gameRepository;
    private readonly IPublisher _publisher;
    private readonly ILogger<VillainSkipPlacementCommandHandler> _logger;

    public VillainSkipPlacementCommandHandler(
        IGameRepository gameRepository,
        IPublisher publisher,
        ILogger<VillainSkipPlacementCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<VillainPlacementResult> Handle(VillainSkipPlacementCommand request, CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);

        if (game.VillainId is null)
            throw new DomainException("This game does not have a villain.");

        if (request.VillainPlayerId != game.PlayerTwo)
            throw new UnauthorizedGameAccessException();

        game.SkipPlacement(request.VillainPlayerId);

        // Capture events BEFORE SaveAsync clears them.
        var phaseEvents = game.DomainEvents.OfType<TurnPhaseAdvanced>().ToList();

        await _gameRepository.SaveAsync(game, cancellationToken);

        _logger.LogInformation(
            "Villain skipped placement in game {GameId} on turn {Turn}",
            request.GameId, game.TurnNumber);

        foreach (var e in phaseEvents)
            await _publisher.Publish(
                new TurnPhaseChangedNotification(e.GameId, e.TurnNumber, e.PreviousPhase.ToString(), e.NewPhase?.ToString()),
                cancellationToken);

        return new VillainPlacementResult(game.CurrentPhase?.ToString(), game.MovePhaseActivePlayer?.ToString());
    }
}

