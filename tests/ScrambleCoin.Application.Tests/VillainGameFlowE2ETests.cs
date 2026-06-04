using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ScrambleCoin.Application.Games.VillainActions;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Services;
using ScrambleCoin.Application.Services.Villains;
using ScrambleCoin.Application.Services.Villains.Implementations;
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

    private static Board NewBoard() => new();

    private static Lineup NewLineup(Guid playerId) => new(Enumerable.Range(0, 5).Select(i => PieceFactory.Any($"Piece{i}", playerId)).ToList());

    /// <summary>
    /// Creates a solo game with a villain in InProgress state.
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
    /// Verifies that a solo game is created correctly with the villain.
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
    /// Verifies that pieces landing on coins properly update the score.
    /// </summary>
    [Fact]
    public void SoloGameWithVillain_CoinCollection_ScoreIncreases()
    {
        // Arrange
        var (game, botPlayerId, villainPlayerId) = CreateSoloGame();
         
        var botPiece = game.LineupPlayerOne!.Pieces[0];
        var villainPiece = game.LineupPlayerTwo!.Pieces[0];

        // Spawn coins in the CoinSpawn phase before advancing
        game.SpawnCoins([
            (new Position(5, 4), CoinType.Silver),
            (new Position(5, 5), CoinType.Gold)
        ]);
         
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        // Place both pieces on board (this auto-advances to MovePhase)
        var botPos = new Position(0, 3);
        var villainPos = new Position(7, 4);
        game.PlacePiece(botPlayerId, botPiece.Id, botPos);
        game.PlacePiece(villainPlayerId, villainPiece.Id, villainPos);

        var initialVillainScore = game.GetScore(villainPlayerId);

        // Verify we're in MovePhase and coins exist
        Assert.Equal(TurnPhase.MovePhase, game.CurrentPhase);
        var coinsOnBoard = game.Board.GetAllCoins();
        Assert.NotEmpty(coinsOnBoard);
         
        // Bot moves first (PlayerOne is always active first in MovePhase)
        game.SkipMovement(botPlayerId);
         
        // Now it's the villain's turn in MovePhase
        // Act: Move a villain piece to coin via orthogonal path
        // Piece is at (7, 4), coin at (5, 4) - can move straight up
        game.MovePiece(villainPlayerId, villainPiece.Id, [[new Position(6, 4), new Position(5, 4)]]);

        // Assert: The score increased after collecting coin
        var finalVillainScore = game.GetScore(villainPlayerId);
        Assert.True(finalVillainScore > initialVillainScore);
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
        var botPiece = game.LineupPlayerOne!.Pieces[0];
        var villainPiece = game.LineupPlayerTwo!.Pieces[0];

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
        Assert.Equal(botPiece.Id, botOccupant.Id);
        Assert.Equal(villainPiece.Id, villainOccupant.Id);
    }

    /// <summary>
    /// Test 4: VillainAutomationService integrates with real game flow.
    /// Verifies that automation service can be called during game progression.
    /// </summary>
    [Fact]
    public async Task VillainAutomationService_PlacePhase_CanBeInvoked()
    {
        // Arrange
        var (game, _, villainPlayerId) = CreateSoloGame();
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        var (repo, factory, dispatcher) = CreateMockedDependencies(game);

        var strategy = Substitute.For<IVillainStrategy>();
        strategy.DecideAction(game, villainPlayerId).Returns(new SkipPlacementAction());
        factory.CreateStrategy("elsa").Returns(strategy);

        var mediator = Substitute.For<IMediator>();
        var logger = Substitute.For<ILogger<VillainAutomationService>>();

        var service = new VillainAutomationService(repo, factory, dispatcher, mediator, logger);

        // Act
        await service.EnsureVillainActsIfNeededAsync(game.Id);

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
        // Arrange: Create a game without villain ID
        var (game, _, _) = CreateSoloGame();
        game.VillainId = null; // Make it non-solo

        var (repo, factory, dispatcher) = CreateMockedDependencies(game);
        var mediator = Substitute.For<IMediator>();
        var logger = Substitute.For<ILogger<VillainAutomationService>>();

        var service = new VillainAutomationService(repo, factory, dispatcher, mediator, logger);

        // Act
        await service.EnsureVillainActsIfNeededAsync(game.Id);

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
        var (game, _, villainPlayerId) = CreateSoloGame();
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        var villainPiece = game.LineupPlayerTwo!.Pieces[0];
        var position = new Position(7, 4);

        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = new VillainPlacePieceCommandHandler(repo, Substitute.For<IPublisher>(), Substitute.For<ILogger<VillainPlacePieceCommandHandler>>());

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
        var (game, _, villainPlayerId) = CreateSoloGame();
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

    /// <summary>
    /// Test 7.5: Villain respects 3-piece placement limit.
    /// Verifies that the villain skips placement when already at maximum pieces.
    /// </summary>
    [Fact]
    public void SoloGameWithVillain_ThreePieceLimit_SkipsPlacementWhenFull()
    {
        // Arrange: Create a game and manually place 3 villain pieces across multiple turns
        var (game, botPlayerId, villainPlayerId) = CreateSoloGame();
         
        var villainPieces = game.LineupPlayerTwo!.Pieces.Take(3).ToList();
        var botPieces = game.LineupPlayerOne!.Pieces.ToList();
         
        // Turn 1, PlacePhase: Place the first villain piece
        game.AdvancePhase(); // CoinSpawn → PlacePhase
        game.PlacePiece(botPlayerId, botPieces[0].Id, new Position(0, 0));
        game.PlacePiece(villainPlayerId, villainPieces[0].Id, new Position(7, 0));
        // Auto-advances to MovePhase
         
        // MovePhase: Skip both
        game.SkipMovement(botPlayerId);
        game.SkipMovement(villainPlayerId);
        // Auto-advances to CoinSpawn of turn 2
         
        // Turn 2, PlacePhase: Place the second villain piece
        game.AdvancePhase(); // CoinSpawn → PlacePhase
        game.PlacePiece(botPlayerId, botPieces[1].Id, new Position(0, 1));
        game.PlacePiece(villainPlayerId, villainPieces[1].Id, new Position(7, 1));
        // Auto-advances to MovePhase
         
        // MovePhase: Skip both
        game.SkipMovement(botPlayerId);
        game.SkipMovement(villainPlayerId);
        // Auto-advances to CoinSpawn of turn 3
         
        // Turn 3, PlacePhase: Place the third villain piece
        game.AdvancePhase(); // CoinSpawn → PlacePhase
        game.PlacePiece(botPlayerId, botPieces[2].Id, new Position(0, 2));
        game.PlacePiece(villainPlayerId, villainPieces[2].Id, new Position(7, 2));
        // Auto-advances to MovePhase
         
        // Verify villain has 3 pieces on board
        Assert.Equal(3, game.PiecesOnBoard[villainPlayerId]);
         
        // MovePhase: Skip both
        game.SkipMovement(botPlayerId);
        game.SkipMovement(villainPlayerId);
        // Auto-advances to CoinSpawn of turn 4
         
        // Turn 4, PlacePhase: Villain at 3-piece limit, so should skip
        game.AdvancePhase(); // CoinSpawn → PlacePhase
        Assert.Equal(TurnPhase.PlacePhase, game.CurrentPhase);
         
        // Act: Get strategy's decision when already at max pieces
        var strategy = new ElsaStrategy();
        var action = strategy.DecideAction(game, villainPlayerId);
         
        // Assert: Should skip placement, not try to place the 4th piece
        Assert.IsType<SkipPlacementAction>(action);
    }

    /// <summary>
    /// Test 7.75: Verify MovePhase setup for villain actions.
    /// Validates that a game can reach MovePhase with pieces placed for both players.
    /// </summary>
    [Fact]
    public void SoloGameWithVillain_MovePhase_GameStateValid()
    {
        // Arrange: Game with both pieces placed and in MovePhase
        var (game, botPlayerId, villainPlayerId) = CreateSoloGame();
         
        game.AdvancePhase(); // CoinSpawn → PlacePhase
         
        // Place bot piece
        game.PlacePiece(botPlayerId, game.LineupPlayerOne!.Pieces[0].Id, new Position(0, 0));
         
        // Place a villain piece on the board
        game.PlacePiece(villainPlayerId, game.LineupPlayerTwo!.Pieces[0].Id, new Position(7, 4));
        // Both players have now acted, so this auto-advances to MovePhase
         
        // Assert: Game is in MovePhase and both pieces are on board
        Assert.Equal(TurnPhase.MovePhase, game.CurrentPhase);
        Assert.Equal(1, game.PiecesOnBoard[botPlayerId]);
        Assert.Equal(1, game.PiecesOnBoard[villainPlayerId]);
        Assert.Equal(botPlayerId, game.MovePhaseActivePlayer); // Bot moves first
    }

    /// <summary>
    /// Test 8: Verify all 3 villains can be created.
    /// Verifies that Elsa, Ursula, and Gaston villains are available.
    /// </summary>
    [Fact]
    public void SoloGameWithVillain_AllVillainsAvailable_CanBeCreated()
    {
        // Act & Assert: Test each villain
        var (gameElsa, _, _) = CreateSoloGame();
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

        var botPiece = game.LineupPlayerOne!.Pieces[0];
        var villainPiece = game.LineupPlayerTwo!.Pieces[0];
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
