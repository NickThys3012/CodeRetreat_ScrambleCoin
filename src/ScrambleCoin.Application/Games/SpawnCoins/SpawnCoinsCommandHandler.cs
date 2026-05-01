using MediatR;
using ScrambleCoin.Application.Services;

namespace ScrambleCoin.Application.Games.SpawnCoins;

/// <summary>
/// Handles <see cref="SpawnCoinsCommand"/>: delegates all coin-spawn logic to
/// <see cref="CoinSpawnService"/>, which also calls <c>game.AdvancePhase()</c>
/// (CoinSpawn → PlacePhase) and persists the game.
/// </summary>
/// <remarks>
/// This handler is for internal use only (e.g. triggered automatically when a game enters
/// the CoinSpawn phase). It is NOT exposed as a bot-accessible REST endpoint.
/// </remarks>
public sealed class SpawnCoinsCommandHandler : IRequestHandler<SpawnCoinsCommand>
{
    private readonly CoinSpawnService _coinSpawnService;

    /// <param name="coinSpawnService">Service that encapsulates the full coin-spawn workflow.</param>
    public SpawnCoinsCommandHandler(CoinSpawnService coinSpawnService)
    {
        _coinSpawnService = coinSpawnService;
    }

    public Task Handle(SpawnCoinsCommand request, CancellationToken cancellationToken) =>
        _coinSpawnService.ExecuteAsync(request.GameId, cancellationToken);
}

