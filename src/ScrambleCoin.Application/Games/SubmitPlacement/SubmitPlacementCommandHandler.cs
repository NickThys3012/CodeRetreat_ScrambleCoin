using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Notifications;
using ScrambleCoin.Application.Services;
using ScrambleCoin.Domain.Events;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Games.SubmitPlacement;

/// <summary>
/// Handles <see cref="SubmitPlacementCommand"/>: resolves the bot token to a player,
/// validates the action, delegates to the domain, persists the game, and returns the resulting phase state.
/// </summary>
public sealed class SubmitPlacementCommandHandler : IRequestHandler<SubmitPlacementCommand, PlacementResult>
{
    private readonly IGameRepository _gameRepository;
    private readonly IBotRegistrationRepository _botRegistrationRepository;
    private readonly IPublisher _publisher;
    private readonly IVillainAutomationService _villainAutomationService;
    private readonly ILogger<SubmitPlacementCommandHandler> _logger;

    public SubmitPlacementCommandHandler(
        IGameRepository gameRepository,
        IBotRegistrationRepository botRegistrationRepository,
        IPublisher publisher,
        IVillainAutomationService villainAutomationService,
        ILogger<SubmitPlacementCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _botRegistrationRepository = botRegistrationRepository;
        _publisher = publisher;
        _villainAutomationService = villainAutomationService;
        _logger = logger;
    }

    public async Task<PlacementResult> Handle(SubmitPlacementCommand request, CancellationToken cancellationToken)
    {
        var registration = await _botRegistrationRepository.GetByTokenAsync(request.BotToken, cancellationToken);
        if (registration is null || registration.GameId != request.GameId)
            throw new UnauthorizedGameAccessException();

        var playerId = registration.PlayerId;
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);

        switch (request.Action?.ToLowerInvariant())
        {
            case "place":
                if (request.PieceId is null || request.Position is null)
                    throw new DomainException("Action 'place' requires 'pieceId' and 'position'.");
                game.PlacePiece(
                    playerId,
                    request.PieceId.Value,
                    new Position(request.Position.Row, request.Position.Col));
                break;

            case "replace":
                if (request.PieceId is null || request.ReplacedPieceId is null)
                    throw new DomainException("Action 'replace' requires 'pieceId' and 'replacedPieceId'.");
                game.ReplacePiece(
                    playerId,
                    request.ReplacedPieceId.Value,
                    request.PieceId.Value);
                break;

            case "skip":
                game.SkipPlacement(playerId);
                break;

            default:
                throw new DomainException($"Unknown action '{request.Action}'. Valid values are: place, replace, skip.");
        }

        // Capture domain events BEFORE SaveAsync clears them.
        var phaseAdvancedEvents = game.DomainEvents
            .OfType<TurnPhaseAdvanced>()
            .ToList();

        await _gameRepository.SaveAsync(game, cancellationToken);

        _logger.LogInformation(
            "Placement action '{Action}' committed by player {PlayerId} in game {GameId} on turn {Turn}",
            request.Action, playerId, request.GameId, game.TurnNumber);

        foreach (var phaseEvent in phaseAdvancedEvents)
            await _publisher.Publish(
                new TurnPhaseChangedNotification(
                    phaseEvent.GameId,
                    phaseEvent.TurnNumber,
                    phaseEvent.PreviousPhase.ToString(),
                    phaseEvent.NewPhase?.ToString()),
                cancellationToken);

        // In solo games, let the CPU villain react after the bot's placement.
        // No-op for non-solo games (the service early-returns when VillainId is null).
        await _villainAutomationService.EnsureVillainActsIfNeededAsync(request.GameId, cancellationToken);

        return new PlacementResult(game.CurrentPhase?.ToString(), game.MovePhaseActivePlayer?.ToString());
    }
}
