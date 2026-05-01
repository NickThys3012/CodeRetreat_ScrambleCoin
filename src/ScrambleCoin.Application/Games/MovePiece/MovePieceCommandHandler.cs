using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Notifications;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Events;

namespace ScrambleCoin.Application.Games.MovePiece;

/// <summary>
/// Handles <see cref="MovePieceCommand"/>: loads the game, delegates movement to the domain,
/// and persists the updated state.
/// When the turn rolls over the domain raises a <see cref="TurnPhaseAdvanced"/> event with
/// <c>NewPhase == CoinSpawn</c>. This handler translates that signal into a
/// <see cref="TurnRolledOver"/> MediatR notification, which <see cref="TurnRolledOverHandler"/>
/// reacts to — keeping coin-spawn logic out of this handler entirely.
/// </summary>
public sealed class MovePieceCommandHandler : IRequestHandler<MovePieceCommand>
{
    private readonly IGameRepository _gameRepository;
    private readonly IPublisher _publisher;
    private readonly ILogger<MovePieceCommandHandler> _logger;

    public MovePieceCommandHandler(
        IGameRepository gameRepository,
        IPublisher publisher,
        ILogger<MovePieceCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _publisher = publisher;
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

        // Capture whether the turn rolled over BEFORE SaveAsync clears domain events.
        var turnRolledOver = game.DomainEvents
            .OfType<TurnPhaseAdvanced>()
            .Any(e => e.NewPhase == TurnPhase.CoinSpawn);

        await _gameRepository.SaveAsync(game, cancellationToken);

        if (turnRolledOver)
            await _publisher.Publish(new TurnRolledOver(request.GameId), cancellationToken);
    }
}
