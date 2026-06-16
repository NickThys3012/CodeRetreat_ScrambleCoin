using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Notifications;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Events;

namespace ScrambleCoin.Application.Games.Admin;

/// <summary>
/// Handles <see cref="ForceEndGameCommand"/>: loads the game, force-ends it, and
/// publishes <see cref="GameFinished"/> so that ranking points and SignalR notifications
/// are applied via the existing notification pipeline.
/// </summary>
public sealed class ForceEndGameCommandHandler : IRequestHandler<ForceEndGameCommand>
{
    private readonly IGameRepository _gameRepository;
    private readonly IPublisher _publisher;
    private readonly ILogger<ForceEndGameCommandHandler> _logger;

    public ForceEndGameCommandHandler(
        IGameRepository gameRepository,
        IPublisher publisher,
        ILogger<ForceEndGameCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Handle(ForceEndGameCommand request, CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);

        if (game.Status is GameStatus.Finished or GameStatus.Cancelled)
        {
            _logger.LogWarning(
                "Admin requested ForceEnd for game {GameId} which is already {Status}. Skipping.",
                request.GameId, game.Status);
            return;
        }

        GameEnded? gameEndedEvent = null;

        if (game.Status == GameStatus.InProgress)
        {
            // End the game by current coin scores.
            // The timed-out bot will typically have fewer coins because it missed moves.
            game.End();
            gameEndedEvent = game.DomainEvents.OfType<GameEnded>().FirstOrDefault();
        }
        else
        {
            // WaitingForBots — cancel the lobby (no ranking points awarded).
            game.ForceCancel();
        }

        // SaveAsync clears domain events — capture the event before saving.
        await _gameRepository.SaveAsync(game, cancellationToken);

        _logger.LogInformation(
            "Admin force-ended game {GameId}. WinnerId={WinnerId}, IsDraw={IsDraw}.",
            request.GameId,
            gameEndedEvent?.WinnerId?.ToString() ?? "N/A",
            gameEndedEvent?.IsDraw ?? false);

        if (gameEndedEvent is not null)
        {
            // Trigger ranking-points award and SignalR broadcast via existing handlers.
            await _publisher.Publish(
                new GameFinished(game.Id, gameEndedEvent.WinnerId, gameEndedEvent.IsDraw),
                cancellationToken);
        }
    }
}
