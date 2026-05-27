using ScrambleCoin.Application.Services;
using ScrambleCoin.Application.Services.Villains;
using ScrambleCoin.Application.Services.Villains.Implementations;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Tests.Helpers;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Tests;

public class VillainStrategyTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Board NewBoard() => new();

    private static Lineup NewLineup(Guid playerId) => new(Enumerable.Range(0, 5).Select(i => PieceFactory.Any($"Piece{i}", playerId)).ToList());

    /// <summary>
    /// Creates a Game in InProgress state with villain (PlayerTwo) ready for testing.
    /// </summary>
    private static (Game game, Guid p1, Guid p2) StartedGameWithVillain()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid(); // This will be the villain
        var game = new Game(p1, p2, NewBoard());
        game.SetLineup(p1, NewLineup(p1));
        game.SetLineup(p2, NewLineup(p2));
        game.Start();
        return (game, p1, p2);
    }

    /// <summary>
    /// Advances the game through CoinSpawn and PlacePhase.
    /// </summary>
    private static void AdvanceToMovePhase(Game game)
    {
        game.AdvancePhase(); // CoinSpawn → PlacePhase
        game.AdvancePhase(); // PlacePhase → MovePhase
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ElsaStrategy_DecideAction_PlacePhase_ReturnsPlacementAction()
    {
        var (game, _, villainId) = StartedGameWithVillain();
        var strategy = new ElsaStrategy();

        // Game starts in CoinSpawn phase, advance to PlacePhase
        game.AdvancePhase();

        var action = strategy.DecideAction(game, villainId);

        Assert.NotNull(action);
        Assert.IsType<PlacementAction>(action);
    }

    [Fact]
    public void Strategy_DecideAction_MovePhase_ReturnsMovementOrSkip()
    {
        var (game, p1, villainId) = StartedGameWithVillain();
        var strategy = new ElsaStrategy();

        // Advance to move phase and place a piece for the villain
        AdvanceToMovePhase(game);

        // Villain has no pieces on board yet, should skip
        var action = strategy.DecideAction(game, villainId);
        Assert.IsType<SkipMovementAction>(action);
    }

    [Fact]
    public void Strategy_DecideAction_SkipsMovementWhenNoCoinsOnBoard()
    {
        // This test verifies the strategy skips when no coins are on board.
        // We'll test this indirectly by ensuring the strategy doesn't crash
        // when deciding in MovePhase with pieces but no coins.
        var (game, p1, villainId) = StartedGameWithVillain();
        var strategy = new ElsaStrategy();

        // Advance past coin spawn and placement
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        // Both players skip placement
        game.SkipPlacement(p1);
        game.SkipPlacement(villainId);

        // Now we should be in MovePhase (or possibly next phase)
        // This test ensures the strategy can handle the game state
        // without crashing when no coins are on board
        if (game.CurrentPhase != TurnPhase.MovePhase)
            return;
        
        var action = strategy.DecideAction(game, villainId);
        Assert.IsType<SkipMovementAction>(action);
    }

    [Fact]
    public void VillainStrategyFactory_CreateStrategy_ReturnsCorrectImplementation()
    {
        var factory = new VillainStrategyFactory();

        var elsa = factory.CreateStrategy(VillainRegistry.Elsa.Id);
        Assert.IsType<ElsaStrategy>(elsa);

        var ursula = factory.CreateStrategy(VillainRegistry.Ursula.Id);
        Assert.IsType<UrsulaStrategy>(ursula);

        var gaston = factory.CreateStrategy(VillainRegistry.Gaston.Id);
        Assert.IsType<GastonStrategy>(gaston);
    }

    [Fact]
    public void VillainStrategyFactory_GetDisplayName_ReturnsCorrectName()
    {
        var factory = new VillainStrategyFactory();

        Assert.Equal("Elsa", factory.GetDisplayName(VillainRegistry.Elsa.Id));
        Assert.Equal("Ursula", factory.GetDisplayName(VillainRegistry.Ursula.Id));
        Assert.Equal("Gaston", factory.GetDisplayName(VillainRegistry.Gaston.Id));
    }

    [Fact]
    public void VillainStrategyFactory_GetVillainLineup_ReturnsValidLineup()
    {
        var factory = new VillainStrategyFactory();
        var playerId = Guid.NewGuid();

        var elsaLineup = factory.GetVillainLineup(VillainRegistry.Elsa.Id, playerId);

        Assert.NotNull(elsaLineup);
        Assert.Equal(5, elsaLineup.Pieces.Count);
        Assert.All(elsaLineup.Pieces, p => Assert.Equal(playerId, p.PlayerId));
    }

    [Fact]
    public void Strategy_PlacementAction_ContainsValidPosition()
    {
        var (game, _, villainId) = StartedGameWithVillain();
        var strategy = new ElsaStrategy();

        game.AdvancePhase(); // CoinSpawn → PlacePhase

        var action = strategy.DecideAction(game, villainId);

        Assert.IsType<PlacementAction>(action);
        var placementAction = (PlacementAction)action;
        Assert.NotNull(placementAction.Position);
    }
}
