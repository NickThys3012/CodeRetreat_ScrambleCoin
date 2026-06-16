using Microsoft.Extensions.Logging;
using NSubstitute;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Services;
using ScrambleCoin.Application.Services.Villains;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Unit tests for <see cref="VillainAutomationService"/> (Issue #41).
/// Verifies: no-op for non-solo games, driving the villain's MovePhase until control returns, and
/// failure isolation (a failed villain action falls back to a skip and never rethrows into the
/// bot's shared request path).
/// </summary>
public class VillainAutomationServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a real solo game (bot = PlayerOne, villain = PlayerTwo) advanced to the villain's
    /// MovePhase with one Mickey on the board and the bot holding no on-board pieces.
    /// </summary>
    private static (Game game, Guid villain) SoloGameInVillainMovePhase()
    {
        var bot = Guid.NewGuid();
        var villain = Guid.NewGuid();

        var game = new Game(Guid.NewGuid(), bot, villain, new Board())
        {
            GameMode = GameMode.Solo,
            VillainId = VillainRegistry.Elsa.Id
        };

        var villainLineup = VillainRegistry.Elsa.GetLineup(villain);
        game.SetLineup(bot, VillainRegistry.GetDefaultLineup(bot));
        game.SetLineup(villain, villainLineup);

        game.Start();        // → CoinSpawn, turn 1
        game.AdvancePhase(); // → PlacePhase
        game.PlacePiece(villain, villainLineup.Pieces[0].Id, new Position(0, 0));
        game.SkipPlacement(bot); // → MovePhase; bot has 0 pieces, so the villain becomes active

        return (game, villain);
    }

    private static VillainAutomationService BuildService(
        IGameRepository gameRepo,
        IVillainStrategyFactory factory,
        IVillainActionDispatcher dispatcher) =>
        new(gameRepo, factory, dispatcher, Substitute.For<ILogger<VillainAutomationService>>());

    // ── No-op for non-solo games ────────────────────────────────────────────────

    [Fact]
    public async Task EnsureVillainActsIfNeededAsync_NonSoloGame_DispatchesNothing()
    {
        // Arrange: a standard game without a VillainId.
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = new Game(Guid.NewGuid(), p1, p2, new Board()); // VillainId is null

        var gameRepo = Substitute.For<IGameRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        var factory = Substitute.For<IVillainStrategyFactory>();
        var dispatcher = Substitute.For<IVillainActionDispatcher>();

        var service = BuildService(gameRepo, factory, dispatcher);

        // Act
        await service.EnsureVillainActsIfNeededAsync(game.Id);

        // Assert
        factory.DidNotReceive().CreateStrategy(Arg.Any<string>());
        await dispatcher.DidNotReceive().ExecuteVillainActionAsync(
            Arg.Any<VillainAction>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ── Drives the villain's MovePhase until control returns ────────────────────

    [Fact]
    public async Task EnsureVillainActsIfNeededAsync_VillainMovePhase_DispatchesActionAndReturnsControl()
    {
        // Arrange
        var (game, villain) = SoloGameInVillainMovePhase();
        Assert.Equal(villain, game.MovePhaseActivePlayer); // sanity: it is the villain's turn

        var gameRepo = Substitute.For<IGameRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var strategy = Substitute.For<IVillainStrategy>();
        strategy.DecideAction(Arg.Any<Game>(), Arg.Any<Guid>()).Returns(new SkipMovementAction());
        var factory = Substitute.For<IVillainStrategyFactory>();
        factory.CreateStrategy(VillainRegistry.Elsa.Id).Returns(strategy);

        var dispatcher = Substitute.For<IVillainActionDispatcher>();
        // The dispatched skip actually advances the domain so control leaves the villain.
        dispatcher
            .When(d => d.ExecuteVillainActionAsync(
                Arg.Is<VillainAction>(a => a is SkipMovementAction),
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()))
            .Do(_ => game.SkipMovement(villain));

        var service = BuildService(gameRepo, factory, dispatcher);

        // Act
        await service.EnsureVillainActsIfNeededAsync(game.Id);

        // Assert: exactly one skip dispatched, and the villain is no longer the active mover.
        await dispatcher.Received(1).ExecuteVillainActionAsync(
            Arg.Is<VillainAction>(a => a is SkipMovementAction),
            game.Id, villain, Arg.Any<CancellationToken>());
        Assert.NotEqual(villain, game.MovePhaseActivePlayer);
    }

    // ── Failure isolation: failed action → skip fallback, no rethrow ────────────

    [Fact]
    public async Task EnsureVillainActsIfNeededAsync_DispatchThrows_FallsBackToSkipAndDoesNotRethrow()
    {
        // Arrange
        var (game, villain) = SoloGameInVillainMovePhase();

        var gameRepo = Substitute.For<IGameRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var strategy = Substitute.For<IVillainStrategy>();
        strategy.DecideAction(Arg.Any<Game>(), Arg.Any<Guid>())
            .Returns(new MovementAction(Guid.NewGuid(), new List<IReadOnlyList<Position>>()));
        var factory = Substitute.For<IVillainStrategyFactory>();
        factory.CreateStrategy(VillainRegistry.Elsa.Id).Returns(strategy);

        var dispatcher = Substitute.For<IVillainActionDispatcher>();
        // The "real" villain action fails...
        dispatcher
            .When(d => d.ExecuteVillainActionAsync(
                Arg.Is<VillainAction>(a => a is MovementAction),
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("simulated villain failure"));
        // ...but the skip fallback succeeds and advances the domain.
        dispatcher
            .When(d => d.ExecuteVillainActionAsync(
                Arg.Is<VillainAction>(a => a is SkipMovementAction),
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()))
            .Do(_ => game.SkipMovement(villain));

        var service = BuildService(gameRepo, factory, dispatcher);

        // Act: must NOT throw (the bot shares this call path).
        var ex = await Record.ExceptionAsync(() => service.EnsureVillainActsIfNeededAsync(game.Id));

        // Assert
        Assert.Null(ex);
        await dispatcher.Received(1).ExecuteVillainActionAsync(
            Arg.Is<VillainAction>(a => a is MovementAction),
            game.Id, villain, Arg.Any<CancellationToken>());
        await dispatcher.Received(1).ExecuteVillainActionAsync(
            Arg.Is<VillainAction>(a => a is SkipMovementAction),
            game.Id, villain, Arg.Any<CancellationToken>());
        Assert.NotEqual(villain, game.MovePhaseActivePlayer);
    }
}
