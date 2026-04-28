using MediatR;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Games.SpawnCoins;

/// <summary>
/// Handles <see cref="SpawnCoinsCommand"/>: determines which coins to spawn based on
/// the current turn number, randomly selects free tiles, and delegates to the domain.
/// </summary>
public sealed class SpawnCoinsCommandHandler : IRequestHandler<SpawnCoinsCommand>
{
    private readonly IGameRepository _gameRepository;
    private readonly Random _random;

    /// <param name="gameRepository">Repository for loading and saving games.</param>
    /// <param name="random">
    /// Random instance used for tile selection. Inject <see cref="Random.Shared"/> in
    /// production; inject a seeded instance in tests for deterministic behaviour.
    /// </param>
    public SpawnCoinsCommandHandler(IGameRepository gameRepository, Random random)
    {
        _gameRepository = gameRepository;
        _random = random;
    }

    public async Task Handle(SpawnCoinsCommand request, CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);

        var freeTiles = game.Board.GetFreeTiles();

        // Determine coin spec for this turn.
        var coinSpec = BuildCoinSpec(game.CurrentTurnNumber);

        // Shuffle free tiles using Fisher-Yates.
        var shuffled = freeTiles.ToList();
        for (var i = shuffled.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        // Flatten the coin spec into an ordered list of CoinTypes to place.
        var coinsToPlace = coinSpec.SelectMany(kv => Enumerable.Repeat(kv.CoinType, kv.Count)).ToList();

        // If there are fewer free tiles than coins needed, spawn only as many as possible.
        var spawnCount = Math.Min(coinsToPlace.Count, shuffled.Count);

        var coins = Enumerable.Range(0, spawnCount)
            .Select(i => (shuffled[i].Position, coinsToPlace[i]))
            .ToList();

        game.SpawnCoins(coins);

        await _gameRepository.SaveAsync(game, cancellationToken);
    }

    /// <summary>
    /// Returns the coin allocation for the given turn as an ordered list of (CoinType, Count) pairs.
    /// </summary>
    private List<(CoinType CoinType, int Count)> BuildCoinSpec(int turnNumber)
    {
        return turnNumber switch
        {
            1 => [
                    (CoinType.Silver, _random.Next(7, 10)),   // 7–9 inclusive
                    (CoinType.Silver, _random.Next(2, 5)),    // 2–4 inclusive
                 ],
            2 or 3 => [(CoinType.Silver, _random.Next(2, 5))], // 2–4 inclusive
            4 => [(CoinType.Gold, 4)],
            5 => [(CoinType.Gold, 3)],
            _ => []
        };
    }
}
