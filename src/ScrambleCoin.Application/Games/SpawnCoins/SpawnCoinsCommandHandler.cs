using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Services;

namespace ScrambleCoin.Application.Games.SpawnCoins;

/// <summary>
/// Handles <see cref="SpawnCoinsCommand"/>: determines which coins to spawn based on
/// the current turn number, randomly selects free tiles, and delegates to the domain.
/// </summary>
public sealed class SpawnCoinsCommandHandler : IRequestHandler<SpawnCoinsCommand>
{
    private readonly IGameRepository _gameRepository;
    private readonly Random _random;
    private readonly ILogger<SpawnCoinsCommandHandler> _logger;

    /// <param name="gameRepository">Repository for loading and saving games.</param>
    /// <param name="random">
    /// Random instance used for tile selection. Inject <see cref="Random.Shared"/> in
    /// production; inject a seeded instance in tests for deterministic behaviour.
    /// </param>
    /// <param name="logger">Logger for structured output.</param>
    public SpawnCoinsCommandHandler(
        IGameRepository gameRepository,
        Random random,
        ILogger<SpawnCoinsCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _random = random;
        _logger = logger;
    }

    public async Task Handle(SpawnCoinsCommand request, CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);

        var freeTiles = game.Board.GetFreeTiles();

        // Determine coin types for this turn from the domain schedule.
        var coinsToPlace = CoinSpawnSchedule.For(game.CurrentTurnNumber, _random);

        // Shuffle free tiles using Fisher-Yates.
        var shuffled = freeTiles.ToList();
        for (var i = shuffled.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        // If there are fewer free tiles than coins needed, spawn only as many as possible.
        if (shuffled.Count < coinsToPlace.Count)
            _logger.LogWarning(
                "Not enough free tiles to spawn all scheduled coins for game {GameId} on turn {Turn}. " +
                "Scheduled: {Scheduled}, available: {Available}.",
                request.GameId, game.CurrentTurnNumber, coinsToPlace.Count, shuffled.Count);

        var spawnCount = Math.Min(coinsToPlace.Count, shuffled.Count);

        var positionedCoins = Enumerable.Range(0, spawnCount)
            .Select(i => (shuffled[i].Position, coinsToPlace[i]))
            .ToList();

        game.SpawnCoins(positionedCoins);

        await _gameRepository.SaveAsync(game, cancellationToken);

        _logger.LogInformation(
            "Coins spawned for game {GameId} on turn {Turn}: {Count} coins",
            request.GameId, game.CurrentTurnNumber, positionedCoins.Count);
    }
}

