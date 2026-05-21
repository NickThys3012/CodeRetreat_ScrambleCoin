using MediatR;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Notifications;
using ScrambleCoin.Application.Services;
using ScrambleCoin.Domain.Enums;

namespace ScrambleCoin.Application.Games.SpawnCoins;

/// <summary>
/// Handles <see cref="SpawnCoinsCommand"/>: delegates all coin-spawn logic to
/// <see cref="ICoinSpawnService"/>, which also calls <c>game.AdvancePhase()</c>
/// (CoinSpawn → PlacePhase) and persists the game.
/// After the service completes, checks if the game has finished and publishes a notification if so.
/// </summary>
/// <remarks>
/// This handler is for internal use only (e.g. triggered automatically when a game enters
/// the CoinSpawn phase). It is NOT exposed as a bot-accessible REST endpoint.
/// </remarks>
public sealed class SpawnCoinsCommandHandler : IRequestHandler<SpawnCoinsCommand>
{
    private readonly ICoinSpawnService _coinSpawnService;
    private readonly IGameRepository _gameRepository;
    private readonly IPublisher _publisher;

    /// <param name="coinSpawnService">Service that encapsulates the full coin-spawn workflow.</param>
    /// <param name="gameRepository">Repository for loading games to check if they finished.</param>
    /// <param name="publisher">Publisher for domain notifications.</param>
    public SpawnCoinsCommandHandler(
        ICoinSpawnService coinSpawnService,
        IGameRepository gameRepository,
        IPublisher publisher)
    {
        _coinSpawnService = coinSpawnService;
        _gameRepository = gameRepository;
        _publisher = publisher;
    }

    public async Task Handle(SpawnCoinsCommand request, CancellationToken cancellationToken)
    {
        await _coinSpawnService.ExecuteAsync(request.GameId, cancellationToken);

        // Check if the game finished and publish a notification if it did
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);
        if (game.Status == GameStatus.Finished)
        {
            // Extract winner from the GameEnded domain event (if available)
            var gameEndedEvent = game.DomainEvents
                .OfType<ScrambleCoin.Domain.Events.GameEnded>()
                .FirstOrDefault();

            if (gameEndedEvent != null)
            {
                await _publisher.Publish(
                    new GameFinished(game.Id, gameEndedEvent.WinnerId, gameEndedEvent.IsDraw),
                    cancellationToken);
            }
        }
    }
}

