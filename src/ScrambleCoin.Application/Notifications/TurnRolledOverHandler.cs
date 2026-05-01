using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Services;

namespace ScrambleCoin.Application.Notifications;

/// <summary>
/// Reacts to a <see cref="TurnRolledOver"/> notification by automatically spawning coins
/// for the new turn and advancing the game phase to <c>PlacePhase</c>.
/// </summary>
public sealed class TurnRolledOverHandler : INotificationHandler<TurnRolledOver>
{
    private readonly ICoinSpawnService _coinSpawnService;
    private readonly ILogger<TurnRolledOverHandler> _logger;

    public TurnRolledOverHandler(ICoinSpawnService coinSpawnService, ILogger<TurnRolledOverHandler> logger)
    {
        _coinSpawnService = coinSpawnService;
        _logger = logger;
    }

    public async Task Handle(TurnRolledOver notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Turn rolled over for game {GameId}. Triggering coin spawn.", notification.GameId);

        await _coinSpawnService.ExecuteAsync(notification.GameId, cancellationToken);
    }
}
