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
/// When the game ends the domain raises <see cref="GameEnded"/>; this handler translates that
/// into a <see cref="GameFinished"/> notification so ranking points and other side-effects
/// can be applied.
/// </summary>
public sealed class MovePieceCommandHandler : IRequestHandler<MovePieceCommand, MoveResult>
{
    private readonly IGameRepository _gameRepository;
    private readonly IBotRegistrationRepository _botRegistrationRepository;
    private readonly IPublisher _publisher;
    private readonly IVillainAutomationService _villainAutomationService;
    private readonly ILogger<MovePieceCommandHandler> _logger;

    public MovePieceCommandHandler(
        IGameRepository gameRepository,
        IBotRegistrationRepository botRegistrationRepository,
        IPublisher publisher,
        IVillainAutomationService villainAutomationService,
        ILogger<MovePieceCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _botRegistrationRepository = botRegistrationRepository;
        _publisher = publisher;
        _villainAutomationService = villainAutomationService;
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

        // Capture events BEFORE SaveAsync clears them.
        var phaseAdvancedEvents = game.DomainEvents
            .OfType<TurnPhaseAdvanced>()
            .ToList();

        var turnRolledOver = phaseAdvancedEvents
            .Any(e => e.NewPhase == TurnPhase.CoinSpawn);

        var gameEndedEvent = game.DomainEvents
            .OfType<GameEnded>()
            .FirstOrDefault();

        await _gameRepository.SaveAsync(game, cancellationToken);

        _logger.LogInformation(
            "Piece {PieceId} moved by bot {BotId} in game {GameId} on turn {Turn}.",
            request.PieceId, playerId, request.GameId, turnNumber);

        foreach (var phaseEvent in phaseAdvancedEvents)
            await _publisher.Publish(
                new TurnPhaseChangedNotification(
                    phaseEvent.GameId,
                    phaseEvent.TurnNumber,
                    phaseEvent.PreviousPhase.ToString(),
                    phaseEvent.NewPhase?.ToString()),
                cancellationToken);

        if (turnRolledOver)
            await _publisher.Publish(new TurnRolledOver(request.GameId), cancellationToken);

        if (gameEndedEvent is not null)
            await _publisher.Publish(
                new GameFinished(game.Id, gameEndedEvent.WinnerId, gameEndedEvent.IsDraw),
                cancellationToken);

        // In solo games, let the CPU villain take its move turn once the bot has finished moving.
        // No-op for non-solo games (the service early-returns when VillainId is null).
        await _villainAutomationService.EnsureVillainActsIfNeededAsync(request.GameId, cancellationToken);

        var yourScore = game.Scores.GetValueOrDefault(playerId, 0);
        var opponentId = game.PlayerOne == playerId ? game.PlayerTwo : game.PlayerOne;
        var opponentScore = game.Scores.GetValueOrDefault(opponentId, 0);

        return new MoveResult(
            game.CurrentPhase?.ToString(),
            game.MovePhaseActivePlayer,
            yourScore,
            opponentScore);
    }
}
