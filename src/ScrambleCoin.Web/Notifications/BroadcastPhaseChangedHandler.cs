using MediatR;
using ScrambleCoin.Application.Abstractions;
using ScrambleCoin.Application.Notifications;

namespace ScrambleCoin.Web.Notifications;

/// <summary>
/// Reacts to a <see cref="TurnPhaseChangedNotification"/> by broadcasting the <c>PhaseChanged</c>
/// SignalR event to all spectators watching that game.
/// Lives in the Web layer so it can depend on <see cref="IGameBroadcaster"/> without
/// introducing a SignalR dependency in the Application layer.
/// </summary>
public sealed class BroadcastPhaseChangedHandler : INotificationHandler<TurnPhaseChangedNotification>
{
    private readonly IGameBroadcaster _broadcaster;
    private readonly ILogger<BroadcastPhaseChangedHandler> _logger;

    public BroadcastPhaseChangedHandler(
        IGameBroadcaster broadcaster,
        ILogger<BroadcastPhaseChangedHandler> logger)
    {
        _broadcaster = broadcaster;
        _logger = logger;
    }

    public async Task Handle(TurnPhaseChangedNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            await _broadcaster.BroadcastPhaseChangedAsync(
                gameId: notification.GameId,
                turn: notification.TurnNumber,
                previousPhase: notification.PreviousPhase,
                newPhase: notification.NewPhase,
                ct: cancellationToken);

            // Notify the player(s) who need to act in the new phase.
            // PlacePhase: both players must place/skip → notify both.
            // MovePhase:  the initial active mover → notify them.
            // null / CoinSpawn: no player action needed.
            if (notification.NewPhase is "PlacePhase" or "MovePhase")
                await _broadcaster.NotifyActivePlayersAsync(notification.GameId, cancellationToken);

            _logger.LogInformation(
                "Broadcast PhaseChanged for game {GameId}: turn {Turn}, {Previous} → {New}",
                notification.GameId, notification.TurnNumber,
                notification.PreviousPhase, notification.NewPhase ?? "(game ending)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast PhaseChanged for game {GameId}", notification.GameId);
            // Swallow — broadcast failure must not affect other notification handlers.
        }
    }
}
