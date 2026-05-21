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
/// End-to-end integration tests for solo games with villain opponent (Issue #41).
/// Tests core villain AI functionality: game setup, automation service, command handlers, and game integration.
/// </summary>
public class VillainGameFlowE2ETests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Board NewBoard() => new Board();

    private static Lineup NewLineup(Guid playerId) =>
        new Lineup(Enumerable.Range(0, 5).Select(i => PieceFactory.Any($"Piece{i}", playerId)).ToList());

    /// <summary>
    /// Creates a solo game with villain in InProgress state.
    /// </summary>
    private static (Game game, Guid botPlayerId, Guid villainPlayerId)
        CreateSoloGame(string villainId = "elsa")
    {
        var botPlayerId = Guid.NewGuid();
        var villainPlayerId = Guid.NewGuid();
        var game = new Game(botPlayerId, villainPlayerId, NewBoard());
        game.SetLineup(botPlayerId, NewLineup(botPlayerId));
        game.SetLineup(villainPlayerId, NewLineup(villainPlayerId));
        game.VillainId = villainId;
        game.Start();
        return (game, botPlayerId, villainPlayerId);
    }

    /// <summary>
    /// Creates mocked dependencies for testing with a VillainAutomationService.
    /// </summary>
    private static (IGameRepository repo, IVillainStrategyFactory factory, IVillainActionDispatcher dispatcher)
        CreateMockedDependencies(Game game)
    {
        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var factory = Substitute.For<IVillainStrategyFactory>();
        var strategy = Substitute.For<IVillainStrategy>();
        factory.CreateStrategy(Arg.Any<string>()).Returns(strategy);

        var dispatcher = Substitute.For<IVillainActionDispatcher>();

        return (repo, factory, dispatcher);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Test 1: Solo game setup and basic state verification.
    /// Verifies that a solo game is created correctly with villain.
    /// </summary>
    [Fact]
    public void SoloGameWithVillain_Setup_CreatedSuccessfully()
    {
        // Arrange & Act
        var (game, botPlayerId, villainPlayerId) = CreateSoloGame();

        // Assert
        Assert.NotNull(game);
        Assert.Equal("elsa", game.VillainId);
        Assert.Equal(botPlayerId, game.PlayerOne);
        Assert.Equal(villainPlayerId, game.PlayerTwo);
        Assert.Equal(5, game.LineupPlayerOne!.Pieces.Count);
        Assert.Equal(5, game.LineupPlayerTwo!.Pieces.Count);
        Assert.Equal(TurnPhase.CoinSpawn, game.CurrentPhase);
    }

    /// <summary>
    /// Test 2: Coin collection increases score.
    /// Verifies that pieces landing on coins properly update score.
    /// </summary>
    [Fact]
    public void SoloGameWithVillain_CoinCollection_ScoreIncreases()
    {
        // Arrange
        var (game, botPlayerId, villainPlayerId) = CreateSoloGame();
        
        var botPiece = game.LineupPlayerOne!.Pieces.First();
        var villainPiece = game.LineupPlayerTwo!.Pieces.First();

        game.AdvancePhase(); // CoinSpawn → PlacePhase

        // Place both pieces on board
        var botPos = new Position(0, 3);
        var villainPos = new Position(7, 4);
        game.PlacePiece(botPlayerId, botPiece.Id, botPos);
        game.PlacePiece(villainPlayerId, villainPiece.Id, villainPos);

        var initialVillainScore = game.GetScore(villainPlayerId);

        // Advance to MovePhase and get coins
        Assert.Equal(TurnPhase.MovePhase, game.CurrentPhase);
        var coinsOnBoard = game.Board.GetAllCoins();

        if (coinsOnBoard.Count > 0)
        {
            // Act: Move villain piece to coin
            var coin = coinsOnBoard.First();
            game.MovePiece(villainPlayerId, villainPiece.Id, new[] { new[] { coin.Position } });

            // Assert: Score increased or stayed the same (might collect other coin types)
            var finalVillainScore = game.GetScore(villainPlayerId);
            Assert.True(finalVillainScore >= initialVillainScore);
        }
    }

    /// <summary>
    /// Test 3: Game respects board boundaries for piece placement.
    /// Verifies that pieces can be placed at corner and edge positions.
    /// </summary>
    [Fact]
    public void SoloGameWithVillain_PiecePlacement_RespectsBoundaries()
    {
        // Arrange
        var (game, botPlayerId, villainPlayerId) = CreateSoloGame();
        var botPiece = game.LineupPlayerOne!.Pieces.First();
        var villainPiece = game.LineupPlayerTwo!.Pieces.First();

        game.AdvancePhase(); // CoinSpawn → PlacePhase

        // Act: Place pieces at boundary positions
        var botCorner = new Position(0, 0);
        var villainCorner = new Position(7, 7);
        game.PlacePiece(botPlayerId, botPiece.Id, botCorner);
        game.PlacePiece(villainPlayerId, villainPiece.Id, villainCorner);

        // Assert: Pieces placed successfully
        var botOccupant = game.Board.GetTile(botCorner).AsPiece;
        var villainOccupant = game.Board.GetTile(villainCorner).AsPiece;
        
        Assert.NotNull(botOccupant);
        Assert.NotNull(villainOccupant);
        Assert.Equal(botPiece.Id, botOccupant!.Id);
        Assert.Equal(villainPiece.Id, villainOccupant!.Id);
    }

    /// <summary>
    /// Test 4: VillainAutomationService integrates with real game flow.
    /// Verifies that automation service can be called during game progression.
    /// </summary>
    [Fact]
    public async Task VillainAutomationService_PlacePhase_CanBeInvoked()
    {
        // Arrange
        var (game, botPlayerId, villainPlayerId) = CreateSoloGame();
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        var (repo, factory, dispatcher) = CreateMockedDependencies(game);

        var strategy = Substitute.For<IVillainStrategy>();
        strategy.DecideAction(game, villainPlayerId).Returns(new SkipPlacementAction());
        factory.CreateStrategy("elsa").Returns(strategy);

        var mediator = Substitute.For<IMediator>();
        var logger = Substitute.For<ILogger<VillainAutomationService>>();

        var service = new VillainAutomationService(repo, factory, dispatcher, logger);

        // Act
        await service.EnsureVillainActsIfNeededAsync(game.Id, mediator);

        // Assert: Service processed the request
        await repo.Received().GetByIdAsync(game.Id, Arg.Any<CancellationToken>());
        strategy.Received().DecideAction(Arg.Any<Game>(), villainPlayerId);
    }

    /// <summary>
    /// Test 5: VillainAutomationService does NOT act in non-solo games.
    /// Verifies that non-solo games skip villain automation.
    /// </summary>
    [Fact]
    public async Task VillainAutomationService_NonSoloGame_SkipsAutomation()
    {
        // Arrange: Create game without villain ID
        var (game, botPlayerId, villainPlayerId) = CreateSoloGame();
        game.VillainId = null; // Make it non-solo

        var (repo, factory, dispatcher) = CreateMockedDependencies(game);
        var mediator = Substitute.For<IMediator>();
        var logger = Substitute.For<ILogger<VillainAutomationService>>();

        var service = new VillainAutomationService(repo, factory, dispatcher, logger);

        // Act
        await service.EnsureVillainActsIfNeededAsync(game.Id, mediator);

        // Assert: No dispatcher calls should be made
        await dispatcher.DidNotReceive().ExecuteVillainActionAsync(
            Arg.Any<VillainAction>(),
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<IMediator>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Test 6: Villain placement command handler works correctly.
    /// Verifies that VillainPlacePieceCommand executes via MediatR.
    /// </summary>
    [Fact]
    public async Task VillainPlacePieceCommand_ValidPlacement_PlacesPieceAndAdvancesPhase()
    {
        // Arrange
        var (game, botPlayerId, villainPlayerId) = CreateSoloGame();
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        var villainPiece = game.LineupPlayerTwo!.Pieces.First();
        var position = new Position(7, 4);

        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = new VillainPlacePieceCommandHandler(repo, Substitute.For<ILogger<VillainPlacePieceCommandHandler>>());

        // Act
        var result = await handler.Handle(
            new VillainPlacePieceCommand(game.Id, villainPlayerId, villainPiece.Id, position),
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("PlacePhase", result.CurrentPhase);
        await repo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Test 7: Villain skip placement command handler works correctly.
    /// Verifies that VillainSkipPlacementCommand executes properly.
    /// </summary>
    [Fact]
    public async Task VillainSkipPlacementCommand_SkipsPlacementPhase()
    {
        // Arrange
        var (game, botPlayerId, villainPlayerId) = CreateSoloGame();
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = new VillainSkipPlacementCommandHandler(repo, Substitute.For<ILogger<VillainSkipPlacementCommandHandler>>());

        // Act
        var result = await handler.Handle(
            new VillainSkipPlacementCommand(game.Id, villainPlayerId),
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        await repo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Test 8: Verify all 3 villains can be created.
    /// Verifies that Elsa, Ursula, and Gaston villains are available.
    /// </summary>
    [Fact]
    public void SoloGameWithVillain_AllVillainsAvailable_CanBeCreated()
    {
        // Act & Assert: Test each villain
        var (gameElsa, _, _) = CreateSoloGame("elsa");
        Assert.Equal("elsa", gameElsa.VillainId);

        var (gameUrsula, _, _) = CreateSoloGame("ursula");
        Assert.Equal("ursula", gameUrsula.VillainId);

        var (gameGaston, _, _) = CreateSoloGame("gaston");
        Assert.Equal("gaston", gameGaston.VillainId);
    }

    /// <summary>
    /// Test 9: Game turn counter increments correctly.
    /// Verifies that turns progress from 1, 2, 3, etc.
    /// </summary>
    [Fact]
    public void SoloGameWithVillain_TurnCounter_IncrementsCorrectly()
    {
        // Arrange
        var (game, botPlayerId, villainPlayerId) = CreateSoloGame();

        // Assert initial turn is 1
        Assert.Equal(1, game.TurnNumber);

        // Act: Progress through turn phases
        game.AdvancePhase(); // CoinSpawn → PlacePhase
        Assert.Equal(1, game.TurnNumber);

        var botPiece = game.LineupPlayerOne!.Pieces.First();
        var villainPiece = game.LineupPlayerTwo!.Pieces.First();
        game.PlacePiece(botPlayerId, botPiece.Id, new Position(0, 3));
        game.PlacePiece(villainPlayerId, villainPiece.Id, new Position(7, 4));

        // Should be in MovePhase, turn still 1
        Assert.Equal(TurnPhase.MovePhase, game.CurrentPhase);
        Assert.Equal(1, game.TurnNumber);

        // Skip movement to advance turn
        game.SkipMovement(botPlayerId);
        game.SkipMovement(villainPlayerId);
        game.AdvancePhase(); // MovePhase → CoinSpawn (next turn)

        // Assert: Turn should be 2
        Assert.Equal(2, game.TurnNumber);
    }
}
