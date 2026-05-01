using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Services;

namespace ScrambleCoin.Application.Services;

/// <summary>
/// Encapsulates the coin-spawn logic for a single turn: determines which coins to place,
/// randomly selects free tiles, delegates to the domain, advances the game phase, and persists.
/// </summary>
/// <remarks>
/// This service is called internally by the application layer when a game enters the
/// <see cref="ScrambleCoin.Domain.Enums.TurnPhase.CoinSpawn"/> phase — it is NOT
/// exposed as a bot-accessible endpoint.
/// </remarks>
public sealed class CoinSpawnService : ICoinSpawnService
{
    private readonly IGameRepository _gameRepository;
    private readonly Random _random;
    private readonly ILogger<CoinSpawnService> _logger;

    /// <param name="gameRepository">Repository for loading and saving games.</param>
    /// <param name="random">
    /// Random instance used for tile selection and coin count. Inject <see cref="Random.Shared"/>
    /// in production; inject a seeded instance in tests for deterministic behaviour.
    /// </param>
    /// <param name="logger">Logger for structured output.</param>
    public CoinSpawnService(
        IGameRepository gameRepository,
        Random random,
        ILogger<CoinSpawnService> logger)
    {
        _gameRepository = gameRepository;
        _random = random;
        _logger = logger;
    }

    /// <summary>
    /// Loads the game by <paramref name="gameId"/>, spawns coins for the current turn,
    /// advances the phase from <c>CoinSpawn</c> → <c>PlacePhase</c>, and saves.
    /// </summary>
    public async Task ExecuteAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var game = await _gameRepository.GetByIdAsync(gameId, cancellationToken);
        await ExecuteForGameAsync(game, cancellationToken);
    }

    /// <summary>
    /// Spawns coins for the current turn on an already-loaded <paramref name="game"/>,
    /// advances the phase from <c>CoinSpawn</c> → <c>PlacePhase</c>, and saves.
    /// Use this overload when the caller has already loaded the game to avoid a redundant
    /// repository round-trip.
    /// </summary>
    public async Task ExecuteForGameAsync(Game game, CancellationToken cancellationToken = default)
    {
        var freeTiles = game.Board.GetFreeTiles();

        // Determine coin types for this turn from the domain schedule.
        var coinsToPlace = CoinSpawnSchedule.For(game.CurrentTurnNumber, _random);

        // Fisher-Yates shuffle of free tiles for random placement.
        var shuffled = freeTiles.ToList();
        for (var i = shuffled.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        // If fewer free tiles exist than coins scheduled, spawn only as many as possible.
        if (shuffled.Count < coinsToPlace.Count)
            _logger.LogWarning(
                "Not enough free tiles to spawn all scheduled coins for game {GameId} on turn {Turn}. " +
                "Scheduled: {Scheduled}, available: {Available}.",
                game.Id, game.CurrentTurnNumber, coinsToPlace.Count, shuffled.Count);

        var spawnCount = Math.Min(coinsToPlace.Count, shuffled.Count);

        var positionedCoins = Enumerable.Range(0, spawnCount)
            .Select(i => (shuffled[i].Position, coinsToPlace[i]))
            .ToList();

        game.SpawnCoins(positionedCoins);

        // CoinSpawn → PlacePhase
        game.AdvancePhase();

        await _gameRepository.SaveAsync(game, cancellationToken);

        _logger.LogInformation(
            "Coins spawned for game {GameId} on turn {Turn}: {Count} coin(s). Phase advanced to {Phase}.",
            game.Id, game.CurrentTurnNumber, positionedCoins.Count, game.CurrentPhase);
    }
}
