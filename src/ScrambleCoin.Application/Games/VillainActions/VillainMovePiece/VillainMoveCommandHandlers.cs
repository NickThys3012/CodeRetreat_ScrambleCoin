using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Notifications;
using ScrambleCoin.Domain.Events;
using ScrambleCoin.Domain.Exceptions;
namespace ScrambleCoin.Application.Games.VillainActions.VillainMovePiece;

/// <summary>
/// Handles <see cref="VillainMovePieceCommand"/>: moves a piece for the villain.
/// </summary>
public sealed class VillainMovePieceCommandHandler : IRequestHandler<VillainMovePieceCommand, VillainMoveResultDto>
{
    private readonly IGameRepository _gameRepository;
    private readonly IPublisher _publisher;
    private readonly ILogger<VillainMovePieceCommandHandler> _logger;

    public VillainMovePieceCommandHandler(
        IGameRepository gameRepository,
        IPublisher publisher,
        ILogger<VillainMovePieceCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<VillainMoveResultDto> Handle(VillainMovePieceCommand request, CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);

        if (game.VillainId is null)
            throw new DomainException("This game does not have a villain.");

        if (game.MovePhaseActivePlayer != request.VillainPlayerId)
            throw new UnauthorizedGameAccessException();

        game.MovePiece(request.VillainPlayerId, request.PieceId, request.Segments);

        // Capture events BEFORE SaveAsync clears them.
        var phaseEvents = game.DomainEvents.OfType<TurnPhaseAdvanced>().ToList();
        var gameEndedEvent = game.DomainEvents.OfType<GameEnded>().FirstOrDefault();

        await _gameRepository.SaveAsync(game, cancellationToken);

        _logger.LogInformation(
            "Villain moved piece {PieceId} in game {GameId} during turn {Turn}",
            request.PieceId, request.GameId, game.TurnNumber);

        foreach (var e in phaseEvents)
            await _publisher.Publish(
                new TurnPhaseChangedNotification(e.GameId, e.TurnNumber, e.PreviousPhase.ToString(), e.NewPhase?.ToString()),
                cancellationToken);

        if (gameEndedEvent is not null)
            await _publisher.Publish(
                new GameFinished(game.Id, gameEndedEvent.WinnerId, gameEndedEvent.IsDraw),
                cancellationToken);

        return new VillainMoveResultDto(game.CurrentPhase?.ToString(), game.MovePhaseActivePlayer?.ToString());
    }
}
