using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Services;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Games.SubmitPlacement;

/// <summary>
/// Handles <see cref="SubmitPlacementCommand"/>: resolves the bot token to a player,
/// validates the action, delegates to the domain, persists the game, and returns the resulting phase state.
/// After execution, triggers villain automation if needed.
/// </summary>
public sealed class SubmitPlacementCommandHandler : IRequestHandler<SubmitPlacementCommand, PlacementResult>
{
    private readonly IGameRepository _gameRepository;
    private readonly IBotRegistrationRepository _botRegistrationRepository;
    private readonly IVillainAutomationService _villainAutomationService;
    private readonly ILogger<SubmitPlacementCommandHandler> _logger;

    public SubmitPlacementCommandHandler(
        IGameRepository gameRepository,
        IBotRegistrationRepository botRegistrationRepository,
        IVillainAutomationService villainAutomationService,
        ILogger<SubmitPlacementCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _botRegistrationRepository = botRegistrationRepository;
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

        await _gameRepository.SaveAsync(game, cancellationToken);

        _logger.LogInformation(
            "Placement action '{Action}' committed by player {PlayerId} in game {GameId} on turn {Turn}",
            request.Action, playerId, request.GameId, game.TurnNumber);

        // Trigger villain automation if it's now the villain's turn
        await _villainAutomationService.EnsureVillainActsIfNeededAsync(request.GameId, cancellationToken);

        return new PlacementResult(game.CurrentPhase?.ToString(), game.MovePhaseActivePlayer?.ToString());
    }
}
