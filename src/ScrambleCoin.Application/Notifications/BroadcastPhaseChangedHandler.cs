using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Abstractions;

namespace ScrambleCoin.Application.Notifications;

/// <summary>
/// Reacts to a <see cref="TurnPhaseChangedNotification"/> by broadcasting the <c>PhaseChanged</c>
/// SignalR event to all spectators watching that game.
/// Lives in the Application layer because it only depends on <see cref="IGameBroadcaster"/>,
/// which is an Application abstraction — no SignalR dependency here.
/// </summary>
/// <remarks>
/// <see cref="Behaviours.SignalRBroadcastBehaviour{TRequest,TResponse}"/> already calls
/// <see cref="IGameBroadcaster.NotifyActivePlayersAsync"/> after every
/// <see cref="IGameStateChangingCommand"/>, so this handler deliberately does NOT repeat
/// that call to avoid duplicate <c>ActionRequired</c> events.
/// </remarks>
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
