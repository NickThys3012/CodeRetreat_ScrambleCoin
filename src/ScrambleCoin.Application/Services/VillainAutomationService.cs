using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Services.Villains;
using ScrambleCoin.Domain.Enums;

namespace ScrambleCoin.Application.Services;

/// <summary>
/// Drives the CPU villain's turns in solo games. After a bot acts, the bot-facing command handlers
/// call <see cref="EnsureVillainActsIfNeededAsync"/>, which repeatedly asks the villain strategy for
/// an action and dispatches it (via the same MediatR commands bots use) until control returns to the
/// bot, the phase advances past the villain, or the game ends.
/// </summary>
public interface IVillainAutomationService
{
    /// <summary>
    /// Ensures the villain (PlayerTwo) acts whenever it is its turn in the given game.
    /// No-op for non-solo games (those without a <c>VillainId</c>).
    /// </summary>
    Task EnsureVillainActsIfNeededAsync(Guid gameId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of <see cref="IVillainAutomationService"/>.
/// </summary>
public sealed class VillainAutomationService : IVillainAutomationService
{
    /// <summary>Hard upper bound on villain actions per invocation, guarding against any loop bug.</summary>
    private const int MaxActionsPerInvocation = 32;

    private readonly IGameRepository _gameRepository;
    private readonly IVillainStrategyFactory _villainStrategyFactory;
    private readonly IVillainActionDispatcher _villainActionDispatcher;
    private readonly ILogger<VillainAutomationService> _logger;

    public VillainAutomationService(
        IGameRepository gameRepository,
        IVillainStrategyFactory villainStrategyFactory,
        IVillainActionDispatcher villainActionDispatcher,
        ILogger<VillainAutomationService> logger)
    {
        _gameRepository = gameRepository;
        _villainStrategyFactory = villainStrategyFactory;
        _villainActionDispatcher = villainActionDispatcher;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task EnsureVillainActsIfNeededAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var game = await _gameRepository.GetByIdAsync(gameId, cancellationToken);

        // Only solo games have a CPU villain.
        if (game.VillainId is null)
            return;

        var villainId = game.VillainId;
        if (!VillainRegistry.IsKnown(villainId))
        {
            _logger.LogWarning(
                "Game {GameId} has unknown villain '{VillainId}'; skipping villain automation.",
                gameId, villainId);
            return;
        }

        var strategy = _villainStrategyFactory.CreateStrategy(villainId);
        var villainPlayerId = game.PlayerTwo;

        // Tracks whether the villain has already acted during the current PlacePhase, to guarantee
        // exactly one placement/skip per PlacePhase (the domain throws on a double placement).
        var actedInCurrentPlacePhase = false;

        for (var iteration = 0; iteration < MaxActionsPerInvocation; iteration++)
        {
            game = await _gameRepository.GetByIdAsync(gameId, cancellationToken);

            if (game.Status != GameStatus.InProgress)
                break;

            var isVillainsTurn = game.CurrentPhase switch
            {
                // A player may act exactly once per PlacePhase. The villain acts after the bot, so a
                // single villain action advances the phase; this flag is a belt-and-braces guard.
                TurnPhase.PlacePhase => !actedInCurrentPlacePhase,
                TurnPhase.MovePhase => game.MovePhaseActivePlayer == villainPlayerId,
                _ => false
            };

            if (!isVillainsTurn)
                break;

            var phaseBeforeAction = game.CurrentPhase;

            try
            {
                var action = strategy.DecideAction(game, villainPlayerId);

                _logger.LogDebug(
                    "Villain {VillainId} acting in game {GameId} (turn {Turn}, phase {Phase}): {Action}",
                    villainId, gameId, game.CurrentTurnNumber, game.CurrentPhase, action.GetType().Name);

                await _villainActionDispatcher.ExecuteVillainActionAsync(action, gameId, villainPlayerId, cancellationToken);
            }
            catch (Exception ex)
            {
                // A villain decision/dispatch failure must never surface to the bot's request (the
                // bot shares this same call path). Swallowing alone would deadlock the game because
                // the villain may still be the active mover, so fall back to skipping the villain's
                // turn to keep control moving.
                _logger.LogError(
                    ex,
                    "Villain action failed in game {GameId} (villain {VillainId}, turn {Turn}); skipping the villain's turn.",
                    gameId, villainId, game.CurrentTurnNumber);

                var skipAction = phaseBeforeAction == TurnPhase.PlacePhase
                    ? (VillainAction)new SkipPlacementAction()
                    : new SkipMovementAction();

                try
                {
                    // Dispatches VillainSkipPlacementCommand (PlacePhase) or VillainSkipMovementCommand
                    // (MovePhase) so the villain advances past its turn instead of deadlocking.
                    await _villainActionDispatcher.ExecuteVillainActionAsync(
                        skipAction, gameId, villainPlayerId, cancellationToken);
                }
                catch (Exception skipEx)
                {
                    // Even the skip failed — stop driving the villain rather than rethrowing into
                    // the bot's request. The action cap / turn guards prevent an infinite loop, but
                    // breaking here avoids spinning on a permanently stuck villain.
                    _logger.LogError(
                        skipEx,
                        "Villain skip fallback failed in game {GameId} (villain {VillainId}, turn {Turn}); abandoning villain automation for this invocation.",
                        gameId, villainId, game.CurrentTurnNumber);
                    break;
                }
            }

            if (phaseBeforeAction == TurnPhase.PlacePhase)
                actedInCurrentPlacePhase = true;
        }

        if (game.Status == GameStatus.InProgress &&
            game.CurrentPhase == TurnPhase.MovePhase &&
            game.MovePhaseActivePlayer == villainPlayerId)
        {
            _logger.LogWarning(
                "Villain automation for game {GameId} hit the action cap ({Cap}) while still the villain's turn.",
                gameId, MaxActionsPerInvocation);
        }
    }
}
