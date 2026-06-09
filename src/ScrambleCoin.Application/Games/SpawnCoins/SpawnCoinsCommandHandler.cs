using MediatR;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Notifications;
using ScrambleCoin.Application.Services;

namespace ScrambleCoin.Application.Games.SpawnCoins;

/// <summary>
/// Handles <see cref="SpawnCoinsCommand"/>: delegates all coin-spawn logic to
/// <see cref="ICoinSpawnService"/>, which also calls <c>game.AdvancePhase()</c>
/// (CoinSpawn → PlacePhase) and persists the game. Publishes a
/// <see cref="TurnPhaseChangedNotification"/> so spectators receive the phase transition.
/// </summary>
/// <remarks>
/// This handler is for internal use only (e.g. triggered automatically when a game enters
/// the CoinSpawn phase). It is NOT exposed as a bot-accessible REST endpoint.
/// Note: games cannot end during the CoinSpawn phase — game-over is only possible at the
/// end of MovePhase, which is handled by <see cref="MovePiece.MovePieceCommandHandler"/>.
/// </remarks>
public sealed class SpawnCoinsCommandHandler : IRequestHandler<SpawnCoinsCommand>
{
    private readonly ICoinSpawnService _coinSpawnService;
    private readonly IGameRepository _gameRepository;
    private readonly IPublisher _publisher;

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
        // Pre-load to capture turn number before the service saves (and clears domain events).
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);
        var turnNumber = game.TurnNumber;

        // ExecuteForGameAsync saves the game — domain events are cleared after this call.
        await _coinSpawnService.ExecuteForGameAsync(game, cancellationToken);

        // CoinSpawn always advances to PlacePhase (games cannot end during coin spawn).
        await _publisher.Publish(
            new TurnPhaseChangedNotification(request.GameId, turnNumber, "CoinSpawn", "PlacePhase"),
            cancellationToken);
    }
}

