using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Notifications;
using ScrambleCoin.Application.Services;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Events;
using ScrambleCoin.Domain.Exceptions;

namespace ScrambleCoin.Application.Games.MovePiece;

/// <summary>
/// Handles <see cref="MovePieceCommand"/>: loads the game, delegates movement to the domain,
/// and persists the updated state.
/// When the turn rolls over the domain raises a <see cref="TurnPhaseAdvanced"/> event with
/// <c>NewPhase == CoinSpawn</c>. This handler translates that signal into a
/// <see cref="TurnRolledOver"/> MediatR notification, which <see cref="TurnRolledOverHandler"/>
/// reacts to — keeping coin-spawn logic out of this handler entirely.
/// After execution, triggers villain automation if needed.
/// </summary>
public sealed class MovePieceCommandHandler : IRequestHandler<MovePieceCommand, MoveResult>
{
    private readonly IGameRepository _gameRepository;
    private readonly IBotRegistrationRepository _botRegistrationRepository;
    private readonly IVillainAutomationService _villainAutomationService;
    private readonly IMediator _mediator;
    private readonly IPublisher _publisher;
    private readonly ILogger<MovePieceCommandHandler> _logger;

    public MovePieceCommandHandler(
        IGameRepository gameRepository,
        IBotRegistrationRepository botRegistrationRepository,
        IVillainAutomationService villainAutomationService,
        IMediator mediator,
        IPublisher publisher,
        ILogger<MovePieceCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _botRegistrationRepository = botRegistrationRepository;
        _villainAutomationService = villainAutomationService;
        _mediator = mediator;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<MoveResult> Handle(MovePieceCommand request, CancellationToken cancellationToken)
    {
        var registration = await _botRegistrationRepository.GetByTokenAsync(request.BotToken, cancellationToken);
        if (registration is null || registration.GameId != request.GameId)
        {
            _logger.LogWarning(
                "Unauthorized move attempt in game {GameId}: token did not resolve to a registered player.",
                request.GameId);
            throw new UnauthorizedGameAccessException();
        }

        var playerId = registration.PlayerId;
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);
        var turnNumber = game.TurnNumber;

        game.MovePiece(playerId, request.PieceId, request.Segments);

        // Capture whether the turn rolled over BEFORE SaveAsync clears domain events.
        var turnRolledOver = game.DomainEvents
            .OfType<TurnPhaseAdvanced>()
            .Any(e => e.NewPhase == TurnPhase.CoinSpawn);

        await _gameRepository.SaveAsync(game, cancellationToken);

        _logger.LogInformation(
            "Piece {PieceId} moved by bot {BotId} in game {GameId} on turn {Turn}.",
            request.PieceId, playerId, request.GameId, turnNumber);

        if (turnRolledOver)
            await _publisher.Publish(new TurnRolledOver(request.GameId), cancellationToken);

        // Trigger villain automation if it's now the villain's turn
        await _villainAutomationService.EnsureVillainActsIfNeededAsync(request.GameId, cancellationToken);

        var yourScore = game.Scores.TryGetValue(playerId, out var s) ? s : 0;
        var opponentId = game.PlayerOne == playerId ? game.PlayerTwo : game.PlayerOne;
        var opponentScore = game.Scores.TryGetValue(opponentId, out var os) ? os : 0;

        return new MoveResult(
            game.CurrentPhase?.ToString(),
            game.MovePhaseActivePlayer,
            yourScore,
            opponentScore);
    }
}
