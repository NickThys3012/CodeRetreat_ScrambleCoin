using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Abstractions;
using ScrambleCoin.Application.Interfaces;

namespace ScrambleCoin.Application.Notifications;

/// <summary>
/// Reacts to a <see cref="GameFinished"/> notification by broadcasting the <c>GameEnded</c>
/// SignalR event with final scores and winner info to all spectators watching that game.
/// Lives in the Application layer because it only depends on Application abstractions
/// (<see cref="IGameBroadcaster"/> and <see cref="IGameRepository"/>) — no SignalR dependency here.
/// </summary>
public sealed class BroadcastGameEndedOnFinishedHandler : INotificationHandler<GameFinished>
{
    private readonly IGameBroadcaster _broadcaster;
    private readonly IGameRepository _gameRepository;
    private readonly ILogger<BroadcastGameEndedOnFinishedHandler> _logger;

    public BroadcastGameEndedOnFinishedHandler(
        IGameBroadcaster broadcaster,
        IGameRepository gameRepository,
        ILogger<BroadcastGameEndedOnFinishedHandler> logger)
    {
        _broadcaster = broadcaster;
        _gameRepository = gameRepository;
        _logger = logger;
    }

    public async Task Handle(GameFinished notification, CancellationToken cancellationToken)
    {
        try
        {
            var game = await _gameRepository.GetByIdAsync(notification.GameId, cancellationToken);
            game.Scores.TryGetValue(game.PlayerOne, out var playerOneScore);
            game.Scores.TryGetValue(game.PlayerTwo, out var playerTwoScore);

            await _broadcaster.BroadcastGameEndedAsync(
                gameId: notification.GameId,
                playerOneScore: playerOneScore,
                playerTwoScore: playerTwoScore,
                winnerId: notification.WinnerId,
                isDraw: notification.IsDraw,
                ct: cancellationToken);

            _logger.LogInformation(
                "Broadcast GameEnded for game {GameId}. P1={P1Score} P2={P2Score} Winner={WinnerId} Draw={IsDraw}",
                notification.GameId, playerOneScore, playerTwoScore,
                notification.WinnerId, notification.IsDraw);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast GameEnded for game {GameId}", notification.GameId);
            // Swallow — broadcast failure must not affect other notification handlers.
        }
    }
}
