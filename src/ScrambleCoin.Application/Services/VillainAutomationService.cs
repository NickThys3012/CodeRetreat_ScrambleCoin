using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Services.Villains;
using ScrambleCoin.Domain.Exceptions;

namespace ScrambleCoin.Application.Services;

/// <summary>
/// Service that automates villain actions during their turns.
/// When it's the villain's turn in PlacePhase or MovePhase, this service:
/// 1. Calls the villain strategy to decide the action
/// 2. Dispatches the appropriate MediatR command to execute the action
/// 3. Repeats until the villain passes or the phase ends
/// </summary>
public interface IVillainAutomationService
{
    /// <summary>
    /// Ensures the villain acts if it's their turn. Processes all villain actions until
    /// the phase advances or the villain passes.
    /// </summary>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    Task EnsureVillainActsIfNeededAsync(Guid gameId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of <see cref="IVillainAutomationService"/>.
/// </summary>
public sealed class VillainAutomationService : IVillainAutomationService
{
    private readonly IGameRepository _gameRepository;
    private readonly IVillainStrategyFactory _villainStrategyFactory;
    private readonly IVillainActionDispatcher _villainActionDispatcher;
    private readonly IMediator _mediator;
    private readonly ILogger<VillainAutomationService> _logger;

    public VillainAutomationService(
        IGameRepository gameRepository,
        IVillainStrategyFactory villainStrategyFactory,
        IVillainActionDispatcher villainActionDispatcher,
        IMediator mediator,
        ILogger<VillainAutomationService> logger)
    {
        _gameRepository = gameRepository;
        _villainStrategyFactory = villainStrategyFactory;
        _villainActionDispatcher = villainActionDispatcher;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task EnsureVillainActsIfNeededAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var game = await _gameRepository.GetByIdAsync(gameId, cancellationToken);

        // Only proceed if this is a solo game with a villain
        if (game.VillainId is null)
            return;

        // Check if it's the villain's turn (villain is PlayerTwo)
        var isVillainsTurn = game.CurrentPhase switch
        {
            Domain.Enums.TurnPhase.PlacePhase => true, // Both players take turns in PlacePhase
            Domain.Enums.TurnPhase.MovePhase => game.MovePhaseActivePlayer == game.PlayerTwo,
            _ => false
        };

        if (!isVillainsTurn)
            return;

        // Get the villain strategy
        var strategy = _villainStrategyFactory.CreateStrategy(game.VillainId);

        // Process villain actions until they pass or the phase ends
        var villainPlayerId = game.PlayerTwo;
        var previousPhase = game.CurrentPhase;
        var maxActionsPerTurn = 10; // Safety limit to prevent infinite loops
        var actionsProcessed = 0;

        while (actionsProcessed < maxActionsPerTurn)
        {
            // Reload the game state to check current conditions
            game = await _gameRepository.GetByIdAsync(gameId, cancellationToken);

            // Check if the phase has advanced or game is no longer active
            if (game.CurrentPhase != previousPhase || game.Status != Domain.Enums.GameStatus.InProgress)
            {
                _logger.LogInformation(
                    "Villain action loop ended: phase changed from {PreviousPhase} to {CurrentPhase}, status={Status}",
                    previousPhase, game.CurrentPhase, game.Status);
                break;
            }

            // Check if it's still the villain's turn
            isVillainsTurn = game.CurrentPhase switch
            {
                Domain.Enums.TurnPhase.PlacePhase => true,
                Domain.Enums.TurnPhase.MovePhase => game.MovePhaseActivePlayer == game.PlayerTwo,
                _ => false
            };

            if (!isVillainsTurn)
            {
                _logger.LogInformation(
                    "Villain action loop ended: not villain's turn. Current phase={Phase}, ActivePlayer={ActivePlayer}",
                    game.CurrentPhase, game.MovePhaseActivePlayer);
                break;
            }

            // Decide the villain's action
            var action = strategy.DecideAction(game, villainPlayerId);
            _logger.LogDebug("Villain {VillainId} decided action: {ActionType}", game.VillainId, action.GetType().Name);

            // Execute the action
            await _villainActionDispatcher.ExecuteVillainActionAsync(action, gameId, villainPlayerId, _mediator, cancellationToken);

            actionsProcessed++;

            // Check for skip actions - if skip, we're done with this turn for the villain
            if (action is SkipPlacementAction or SkipMovementAction)
            {
                _logger.LogInformation(
                    "Villain {VillainId} skipped action ({ActionType}) in game {GameId}",
                    game.VillainId, action.GetType().Name, gameId);
                break;
            }
        }

        if (actionsProcessed >= maxActionsPerTurn)
        {
            _logger.LogWarning(
                "Villain action loop hit safety limit ({MaxActions}) for game {GameId}",
                maxActionsPerTurn, gameId);
        }
    }
}
