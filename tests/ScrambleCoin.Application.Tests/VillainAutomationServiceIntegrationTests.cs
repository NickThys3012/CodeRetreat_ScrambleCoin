using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ScrambleCoin.Application.Games.VillainActions;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Services;
using ScrambleCoin.Application.Services.Villains;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Tests.Helpers;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Integration tests for the VillainAutomationService (Issue #41).
/// Tests the full flow of villain decision-making and action execution.
/// </summary>
public class VillainAutomationServiceIntegrationTests
{
    private static Board NewBoard() => new();

    private static Lineup NewLineup(Guid playerId) => new(Enumerable.Range(0, 5).Select(i => PieceFactory.Any($"Piece{i}", playerId)).ToList());

    /// <summary>
    /// Creates a game with a villain in InProgress state, both lineups set.
    /// </summary>
    private static (Game game, Guid botPlayerId, Guid villainPlayerId, string villainId)
        CreateGameWithVillain(string villainId = "elsa")
    {
        var botPlayerId = Guid.NewGuid();
        var villainPlayerId = Guid.NewGuid();
        var game = new Game(botPlayerId, villainPlayerId, NewBoard());
        game.SetLineup(botPlayerId, NewLineup(botPlayerId));
        game.SetLineup(villainPlayerId, NewLineup(villainPlayerId));
        game.VillainId = villainId;
        game.Start();
        return (game, botPlayerId, villainPlayerId, villainId);
    }

    [Fact]
    public async Task EnsureVillainActsIfNeeded_NonSoloGame_DoesNotTriggerVillain()
    {
        // Arrange: Create a non-solo game (no VillainId)
        var (game, _, _, _) = CreateGameWithVillain();
        game.VillainId = null; // Clear villain ID to make it non-solo

        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var factory = Substitute.For<IVillainStrategyFactory>();
        var dispatcher = Substitute.For<IVillainActionDispatcher>();
        var mediator = Substitute.For<IMediator>();
        var logger = Substitute.For<ILogger<VillainAutomationService>>();

        var service = new VillainAutomationService(repo, factory, dispatcher, mediator, logger);

        // Act
        await service.EnsureVillainActsIfNeededAsync(game.Id);

        // Assert: No actions should be dispatched
        await dispatcher.DidNotReceive().ExecuteVillainActionAsync(
            Arg.Any<VillainAction>(),
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<IMediator>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureVillainActsIfNeeded_PlacePhase_CallsStrategyAndDispatchesAction()
    {
        // Arrange: Game in PlacePhase with villain ready to act
        var (game, _, villainPlayerId, villainId) = CreateGameWithVillain();
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        var expectedAction = new SkipPlacementAction();

        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var strategy = Substitute.For<IVillainStrategy>();
        strategy.DecideAction(game, villainPlayerId).Returns(expectedAction);

        var factory = Substitute.For<IVillainStrategyFactory>();
        factory.CreateStrategy(villainId).Returns(strategy);

        var dispatcher = Substitute.For<IVillainActionDispatcher>();
        var mediator = Substitute.For<IMediator>();
        var logger = Substitute.For<ILogger<VillainAutomationService>>();

        var service = new VillainAutomationService(repo, factory, dispatcher, mediator, logger);

        // Act
        await service.EnsureVillainActsIfNeededAsync(game.Id);

        // Assert
        strategy.Received(1).DecideAction(game, villainPlayerId);
        await dispatcher.Received(1).ExecuteVillainActionAsync(
            Arg.Is<SkipPlacementAction>(a => a == expectedAction),
            game.Id,
            villainPlayerId,
            mediator,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureVillainActsIfNeeded_MovePhase_CallsStrategyWhenVillainIsActive()
    {
        // Arrange: Create a game in PlacePhase (easier to test than MovePhase via public API)
        // and verify the strategy is called. MovePhase testing is harder because
        // MovePhaseActivePlayer starts as PlayerOne, making it hard to test villain's MovePhase turn.
        var (game, botPlayerId, villainPlayerId, villainId) = CreateGameWithVillain();
        game.AdvancePhase(); // CoinSpawn → PlacePhase
         
        // Place pieces to advance to MovePhase
        game.PlacePiece(botPlayerId, game.LineupPlayerOne!.Pieces[0].Id, new Position(0, 0));
        game.PlacePiece(villainPlayerId, game.LineupPlayerTwo!.Pieces[0].Id, new Position(7, 7));
        // Both have now acted, auto-advancing to MovePhase
         
        Assert.Equal(TurnPhase.MovePhase, game.CurrentPhase);
         
        // Skip bot's move to allow villain to move
        game.SkipMovement(botPlayerId);
         
        // Now it's the villain's turn in MovePhase
        Assert.Equal(villainPlayerId, game.MovePhaseActivePlayer);
         
        var expectedAction = new SkipMovementAction();
         
        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
         
        var strategy = Substitute.For<IVillainStrategy>();
        strategy.DecideAction(game, villainPlayerId).Returns(expectedAction);
         
        var factory = Substitute.For<IVillainStrategyFactory>();
        factory.CreateStrategy(villainId).Returns(strategy);
         
        var dispatcher = Substitute.For<IVillainActionDispatcher>();
        var mediator = Substitute.For<IMediator>();
        var logger = Substitute.For<ILogger<VillainAutomationService>>();
         
        var service = new VillainAutomationService(repo, factory, dispatcher, mediator, logger);
         
        // Act
        await service.EnsureVillainActsIfNeededAsync(game.Id);
         
        // Assert: Strategy should be called during villain's MovePhase turn
        strategy.Received(1).DecideAction(game, villainPlayerId);
    }

    [Fact]
    public async Task EnsureVillainActsIfNeeded_SkipActionStopsProcessing()
    {
        // Arrange: Villain decides to skip
        var (game, _, villainPlayerId, villainId) = CreateGameWithVillain();
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        var skipAction = new SkipPlacementAction();

        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var strategy = Substitute.For<IVillainStrategy>();
        strategy.DecideAction(game, villainPlayerId).Returns(skipAction);

        var factory = Substitute.For<IVillainStrategyFactory>();
        factory.CreateStrategy(villainId).Returns(strategy);

        var dispatcher = Substitute.For<IVillainActionDispatcher>();
        var mediator = Substitute.For<IMediator>();
        var logger = Substitute.For<ILogger<VillainAutomationService>>();

        var service = new VillainAutomationService(repo, factory, dispatcher, mediator, logger);

        // Act
        await service.EnsureVillainActsIfNeededAsync(game.Id);

        // Assert: Dispatcher should be called once, then stop (because of skip action)
        await dispatcher.Received(1).ExecuteVillainActionAsync(
            Arg.Any<VillainAction>(),
            game.Id,
            villainPlayerId,
            mediator,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VillainPlacePieceCommand_ValidPlacement_PlacesPieceAndReturnsResult()
    {
        // Arrange
        var (game, _, villainPlayerId, _) = CreateGameWithVillain();
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        var piece = game.LineupPlayerTwo!.Pieces[0];
        var position = new Position(0, 3);

        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = new VillainPlacePieceCommandHandler(repo, Substitute.For<IPublisher>(), Substitute.For<ILogger<VillainPlacePieceCommandHandler>>());

        // Act
        var result = await handler.Handle(
            new VillainPlacePieceCommand(game.Id, villainPlayerId, piece.Id, position),
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("PlacePhase", result.CurrentPhase);
        await repo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VillainSkipPlacementCommand_SkipsPlacement()
    {
        // Arrange
        var (game, _, villainPlayerId, _) = CreateGameWithVillain();
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = new VillainSkipPlacementCommandHandler(repo, Substitute.For<IPublisher>(), Substitute.For<ILogger<VillainSkipPlacementCommandHandler>>());

        // Act
        var result = await handler.Handle(
            new VillainSkipPlacementCommand(game.Id, villainPlayerId),
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        await repo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }
}
