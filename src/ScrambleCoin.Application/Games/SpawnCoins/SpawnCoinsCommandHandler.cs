using MediatR;
using ScrambleCoin.Application.Services;

namespace ScrambleCoin.Application.Games.SpawnCoins;

/// <summary>
/// Handles <see cref="SpawnCoinsCommand"/>: delegates all coin-spawn logic to
/// <see cref="ICoinSpawnService"/>, which also calls <c>game.AdvancePhase()</c>
/// (CoinSpawn → PlacePhase) and persists the game.
/// </summary>
/// <remarks>
/// This handler is for internal use only (e.g. triggered automatically when a game enters
/// the CoinSpawn phase). It is NOT exposed as a bot-accessible REST endpoint.
/// Note: games cannot end during the CoinSpawn phase — game-over is only possible at the
/// end of MovePhase, which is handled by <see cref="MovePieceCommandHandler"/>.
/// </remarks>
public sealed class SpawnCoinsCommandHandler : IRequestHandler<SpawnCoinsCommand>
{
    private readonly ICoinSpawnService _coinSpawnService;

    /// <param name="coinSpawnService">Service that encapsulates the full coin-spawn workflow.</param>
    public SpawnCoinsCommandHandler(ICoinSpawnService coinSpawnService)
    {
        _coinSpawnService = coinSpawnService;
    }

    public async Task Handle(SpawnCoinsCommand request, CancellationToken cancellationToken)
    {
        await _coinSpawnService.ExecuteAsync(request.GameId, cancellationToken);
    }
}

