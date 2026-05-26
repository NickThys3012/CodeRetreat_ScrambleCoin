using System.Collections.ObjectModel;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Application.Games.MovePiece;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Services;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Events;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.Obstacles;
using ScrambleCoin.Domain.ValueObjects;
using DomainBotReg = ScrambleCoin.Domain.BotRegistrations.BotRegistration;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Integration tests for Ethereal movement (Issue #46) via the Application layer.
/// Tests the full flow: game creation → piece placement → ethereal move → score updates.
/// Follows the same pattern as ChargeMovementIntegrationTests.
/// </summary>
public class EtherealMovementIntegrationTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a game in MovePhase with an Ethereal piece for P1 and a normal piece for P2.
    /// </summary>
    private static (Game game, Guid p1, Guid p2, Piece etherealPiece, Piece p2Piece)
        GameInMovePhaseWithEtherealPiece(
            Position? etherealStartPos = null,
            Position? p2StartPos = null,
            int etherealMaxDistance = 3)
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var actualEtherealStartPos = etherealStartPos ?? new Position(0, 0);
        var actualP2StartPos = p2StartPos ?? new Position(7, 7);

        var etherealPiece = new Piece(Guid.NewGuid(), "Ethereal", p1,
            EntryPointType.Corners, MovementType.Ethereal, etherealMaxDistance, 1);
        var p1Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"P1Fill{i}", p1, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var p2Piece = new Piece(Guid.NewGuid(), "P2Mover", p2,
            EntryPointType.Borders, MovementType.Orthogonal, 1, 1);
        var p2Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"P2Fill{i}", p2, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(new[] { etherealPiece }.Concat(p1Fill)));
        game.SetLineup(p2, new Lineup(new[] { p2Piece }.Concat(p2Fill)));
        game.Start();
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        game.PlacePiece(p1, etherealPiece.Id, actualEtherealStartPos);
        game.PlacePiece(p2, p2Piece.Id, actualP2StartPos);

        return (game, p1, p2, etherealPiece, p2Piece);
    }

    /// <summary>
    /// Creates a mock game repository that returns a game by ID and allows saving.
    /// </summary>
    private static IGameRepository MockGameRepository(Game game)
    {
        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        return repo;
    }

    /// <summary>
    /// Creates a mock bot registration repository.
    /// </summary>
    private static IBotRegistrationRepository MockBotRepository(Guid token, Guid playerId, Guid gameId)
    {
        var repo = Substitute.For<IBotRegistrationRepository>();
        repo.GetByTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(new DomainBotReg(token, playerId, gameId));
        return repo;
    }

    /// <summary>
    /// Builds a handler with the provided repositories.
    /// </summary>
    private static MovePieceCommandHandler BuildHandler(
        IGameRepository gameRepo,
        IBotRegistrationRepository botRepo,
        IPublisher? publisher = null)
        => new(gameRepo,
            botRepo,
            Substitute.For<IVillainAutomationService>(),
            publisher ?? Substitute.For<IPublisher>(),
            Substitute.For<ILogger<MovePieceCommandHandler>>());

    /// <summary>
    /// Builds a multistep segment for Ethereal movement.
    /// </summary>
    private static ReadOnlyCollection<IReadOnlyList<Position>> BuildEtherealSegment(params Position[] positions)
    {
        IReadOnlyList<Position> segment = positions.ToList().AsReadOnly();
        return new List<IReadOnlyList<Position>> { segment }.AsReadOnly();
    }

    // ── Integration Test 1: Basic Ethereal movement through rock ──────────────────

    [Fact]
    public async Task EtherealMovement_ViaApplicationLayer_PassThroughRockAndCollectsCoin()
    {
        // Arrange: Ethereal at (0,0), rock at (0,1), coin at (0,2)
        var (game, p1, _, etherealPiece, _) = GameInMovePhaseWithEtherealPiece(
            etherealStartPos: new Position(0, 0),
            etherealMaxDistance: 2);

        var rockPos = new Position(0, 1);
        var coinPos = new Position(0, 2);
        game.Board.AddRock(new Rock(rockPos));
        game.Board.GetTile(coinPos).SetOccupant(new Coin(CoinType.Silver));

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act: Move ethereal through rock to coin tile
        await handler.Handle(
            new MovePieceCommand(game.Id, token, etherealPiece.Id, BuildEtherealSegment(rockPos, coinPos)),
            CancellationToken.None);

        // Assert: the piece moved through rock to coin position
        Assert.Equal(coinPos, etherealPiece.Position);

        // Assert: coin was collected
        Assert.Equal(1, game.Scores[p1]);

        // Assert: coin removed from board
        Assert.Null(game.Board.GetTile(coinPos).AsCoin);

        // Assert: the game was saved
        await gameRepo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }

    // ── Integration Test 2: Ethereal passes through an opponent piece ──────────────────

    [Fact]
    public async Task EtherealMovement_PassThroughOpponentPiece_ReachesDestinationAndSaves()
    {
        // Arrange: Ethereal at (0,0), opponent at (0,1), destination at (0,2)
        var (game, p1, p2, etherealPiece, p2Piece) = GameInMovePhaseWithEtherealPiece(
            etherealStartPos: new Position(0, 0),
            p2StartPos: new Position(0, 1),
            etherealMaxDistance: 2);

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act: Move ethereal through opponent
        await handler.Handle(
            new MovePieceCommand(game.Id, token, etherealPiece.Id, 
                BuildEtherealSegment(new Position(0, 1), new Position(0, 2))),
            CancellationToken.None);

        // Assert: ethereal reached destination
        Assert.Equal(new Position(0, 2), etherealPiece.Position);

        // Assert: an opponent piece still in place (not captured)
        Assert.Equal(new Position(0, 1), p2Piece.Position);

        // Assert: the game was saved
        await gameRepo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }

    // ── Integration Test 3: Ethereal rejects ending on occupied tile ───────────────

    [Fact]
    public async Task EtherealMovement_EndOnOccupiedTile_ThrowsAndDoesNotSave()
    {
        // Arrange: Ethereal at (0,0), opponent at destination (0,1)
        var (game, p1, p2, etherealPiece, p2Piece) = GameInMovePhaseWithEtherealPiece(
            etherealStartPos: new Position(0, 0),
            p2StartPos: new Position(0, 1));

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act & Assert: attempting to end on occupied tile throws
        var ex = await Assert.ThrowsAsync<DomainException>(async () =>
            await handler.Handle(
                new MovePieceCommand(game.Id, token, etherealPiece.Id, BuildEtherealSegment(new Position(0, 1))),
                CancellationToken.None));

        Assert.Contains("free tile", ex.Message);

        // Assert: game was NOT saved (move was rejected)
        await gameRepo.DidNotReceive().SaveAsync(Arg.Any<Game>(), Arg.Any<CancellationToken>());
    }

    // ── Integration Test 4: Ethereal collects all coins in a path ────────────────────

    [Fact]
    public async Task EtherealMovement_CollectsAllCoinsInPath_UpdatesScore()
    {
        // Arrange: Ethereal at (0,0), coins at intermediate and destination tiles
        var (game, p1, _, etherealPiece, _) = GameInMovePhaseWithEtherealPiece(
            etherealStartPos: new Position(0, 0),
            etherealMaxDistance: 3);

        game.Board.GetTile(new Position(0, 1)).SetOccupant(new Coin(CoinType.Silver)); // 1 pt
        game.Board.GetTile(new Position(0, 2)).SetOccupant(new Coin(CoinType.Silver)); // 1 pt
        game.Board.GetTile(new Position(0, 3)).SetOccupant(new Coin(CoinType.Gold));   // 3 pts

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act: Move through all tiles
        await handler.Handle(
            new MovePieceCommand(game.Id, token, etherealPiece.Id,
                BuildEtherealSegment(new Position(0, 1), new Position(0, 2), new Position(0, 3))),
            CancellationToken.None);

        // Assert: all coins collected (1 + 1 + 3 = 5)
        Assert.Equal(5, game.Scores[p1]);

        // Assert: all tiles are now clear
        Assert.Null(game.Board.GetTile(new Position(0, 1)).AsCoin);
        Assert.Null(game.Board.GetTile(new Position(0, 2)).AsCoin);
        Assert.Null(game.Board.GetTile(new Position(0, 3)).AsCoin);

        // Assert: the game was saved
        await gameRepo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }

    // ── Integration Test 5: Ethereal fence blocks movement ────────────────────────

    [Fact]
    public async Task EtherealMovement_FenceBlocksPath_ThrowsAndDoesNotSave()
    {
        // Arrange: Ethereal at (0,0), fence between (0,0) and (0,1)
        var (game, p1, _, etherealPiece, _) = GameInMovePhaseWithEtherealPiece(
            etherealStartPos: new Position(0, 0));

        var fence = new Fence(new Position(0, 0), new Position(0, 1));
        game.Board.AddFence(fence);

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act & Assert: attempting to cross fence throws
        var ex = await Assert.ThrowsAsync<DomainException>(async () =>
            await handler.Handle(
                new MovePieceCommand(game.Id, token, etherealPiece.Id, BuildEtherealSegment(new Position(0, 1))),
                CancellationToken.None));

        Assert.Contains("fence", ex.Message);

        // Assert: the game was NOT saved
        await gameRepo.DidNotReceive().SaveAsync(Arg.Any<Game>(), Arg.Any<CancellationToken>());
    }

    // ── Integration Test 6: Ethereal with multiple obstacles and coins ────────────────

    [Fact]
    public async Task EtherealMovement_MultipleRocksOpponentAndCoins_PassThroughAllAndCollectCoins()
    {
        // Arrange: Ethereal at (0,0), path with rock, opponent, rock, and coin
        var (game, p1, p2, etherealPiece, p2Piece) = GameInMovePhaseWithEtherealPiece(
            etherealStartPos: new Position(0, 0),
            p2StartPos: new Position(0, 2),
            etherealMaxDistance: 4);

        game.Board.AddRock(new Rock(new Position(0, 1))); // Rock at (0,1)
        // Opponent at (0,2) — set via GameInMovePhaseWithEtherealPiece
        game.Board.AddRock(new Rock(new Position(0, 3))); // Rock at (0,3)
        game.Board.GetTile(new Position(0, 4)).SetOccupant(new Coin(CoinType.Silver));

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act: Move through all obstacles
        await handler.Handle(
            new MovePieceCommand(game.Id, token, etherealPiece.Id,
                BuildEtherealSegment(
                    new Position(0, 1),
                    new Position(0, 2),
                    new Position(0, 3),
                    new Position(0, 4))),
            CancellationToken.None);

        // Assert: reached destination
        Assert.Equal(new Position(0, 4), etherealPiece.Position);

        // Assert: coin collected
        Assert.Equal(1, game.Scores[p1]);

        // Assert: opponent still in place
        Assert.Equal(new Position(0, 2), p2Piece.Position);

        // Assert: the game was saved
        await gameRepo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }

    // ── Integration Test 7: Ethereal diagonal movement ─────────────────────────────

    [Fact]
    public async Task EtherealMovement_DiagonalPath_PassesThroughObstaclesAndCollectsCoin()
    {
        // Arrange: Ethereal at (0,0), move diagonally through rock to destination with coin
        var (game, p1, _, etherealPiece, _) = GameInMovePhaseWithEtherealPiece(
            etherealStartPos: new Position(0, 0),
            etherealMaxDistance: 2);

        game.Board.AddRock(new Rock(new Position(1, 1))); // Rock at (1,1)
        game.Board.GetTile(new Position(2, 2)).SetOccupant(new Coin(CoinType.Silver));

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act: Move diagonally through rock to destination
        await handler.Handle(
            new MovePieceCommand(game.Id, token, etherealPiece.Id,
                BuildEtherealSegment(new Position(1, 1), new Position(2, 2))),
            CancellationToken.None);

        // Assert: reached destination
        Assert.Equal(new Position(2, 2), etherealPiece.Position);

        // Assert: coin collected
        Assert.Equal(1, game.Scores[p1]);

        // Assert: the game was saved
        await gameRepo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }

    // ── Integration Test 8: Ethereal end on rock throws ────────────────────────────

    [Fact]
    public async Task EtherealMovement_EndOnRock_ThrowsAndDoesNotSave()
    {
        // Arrange: Ethereal at (0,0), rock at destination (0,1)
        var (game, p1, _, etherealPiece, _) = GameInMovePhaseWithEtherealPiece(
            etherealStartPos: new Position(0, 0));

        game.Board.AddRock(new Rock(new Position(0, 1)));

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act & Assert: attempting to end on rock throws
        var ex = await Assert.ThrowsAsync<DomainException>(async () =>
            await handler.Handle(
                new MovePieceCommand(game.Id, token, etherealPiece.Id, BuildEtherealSegment(new Position(0, 1))),
                CancellationToken.None));

        Assert.Contains("obstacle", ex.Message);

        // Assert: the game was NOT saved
        await gameRepo.DidNotReceive().SaveAsync(Arg.Any<Game>(), Arg.Any<CancellationToken>());
    }

    // ── Integration Test 9: Ethereal end on lake throws ────────────────────────────

    [Fact]
    public async Task EtherealMovement_EndOnLake_ThrowsAndDoesNotSave()
    {
        // Arrange: Ethereal at (0,0), lake at destination (0,1)
        var (game, p1, _, etherealPiece, _) = GameInMovePhaseWithEtherealPiece(
            etherealStartPos: new Position(0, 0));

        game.Board.AddLake(new Lake(new Position(0, 1)));

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act & Assert: attempting to end on lake throws
        var ex = await Assert.ThrowsAsync<DomainException>(async () =>
            await handler.Handle(
                new MovePieceCommand(game.Id, token, etherealPiece.Id, BuildEtherealSegment(new Position(0, 1))),
                CancellationToken.None));

        Assert.Contains("obstacle", ex.Message);

        // Assert: the game was NOT saved
        await gameRepo.DidNotReceive().SaveAsync(Arg.Any<Game>(), Arg.Any<CancellationToken>());
    }

    // ── Integration Test 10: Ethereal respects MaxDistance ────────────────────────

    [Fact]
    public async Task EtherealMovement_ExceedsMaxDistance_ThrowsAndDoesNotSave()
    {
        // Arrange: Ethereal with MaxDistance=2
        var (game, p1, _, etherealPiece, _) = GameInMovePhaseWithEtherealPiece(
            etherealStartPos: new Position(0, 0),
            etherealMaxDistance: 2);

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act & Assert: attempting 3-step movement throws
        var ex = await Assert.ThrowsAsync<DomainException>(async () =>
            await handler.Handle(
                new MovePieceCommand(game.Id, token, etherealPiece.Id,
                    BuildEtherealSegment(
                        new Position(0, 1),
                        new Position(0, 2),
                        new Position(0, 3))),
                CancellationToken.None));

        Assert.Contains("MaxDistance", ex.Message);

        // Assert: the game was NOT saved
        await gameRepo.DidNotReceive().SaveAsync(Arg.Any<Game>(), Arg.Any<CancellationToken>());
    }

    // ── Integration Test 11: Ethereal raises domain events ────────────────────────

    [Fact]
    public async Task EtherealMovement_RaisesCoinCollectedAndPieceMovedEvents()
    {
        // Arrange: Ethereal at (0,0), coins at (0,1) and (0,2)
        var (game, p1, _, etherealPiece, _) = GameInMovePhaseWithEtherealPiece(
            etherealStartPos: new Position(0, 0),
            etherealMaxDistance: 2);

        game.Board.GetTile(new Position(0, 1)).SetOccupant(new Coin(CoinType.Silver));
        game.Board.GetTile(new Position(0, 2)).SetOccupant(new Coin(CoinType.Gold));

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act: Move ethereal
        await handler.Handle(
            new MovePieceCommand(game.Id, token, etherealPiece.Id,
                BuildEtherealSegment(new Position(0, 1), new Position(0, 2))),
            CancellationToken.None);

        // Assert: CoinCollected events raised (2 coins)
        var coinEvents = game.DomainEvents.Where(e => e.GetType().Name == "CoinCollected").ToList();
        Assert.Equal(2, coinEvents.Count);

        // Assert: PieceMoved event raised
        var moveEvent = game.DomainEvents.OfType<PieceMoved>().Single();
        Assert.Equal(new Position(0, 0), moveEvent.From);
        Assert.Equal(new Position(0, 2), moveEvent.To);

        // Assert: the game was saved
        await gameRepo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }

    // ── Integration Test 13: Ethereal end on empty tile (no coin) ──────────────────

    [Fact]
    public async Task EtherealMovement_EndOnEmptyTile_SucceedsAndSaves()
    {
        // Arrange: Ethereal at (0,0), move to empty tile (0,1)
        var (game, p1, _, etherealPiece, _) = GameInMovePhaseWithEtherealPiece(
            etherealStartPos: new Position(0, 0));

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act: Move to an empty tile
        await handler.Handle(
            new MovePieceCommand(game.Id, token, etherealPiece.Id, BuildEtherealSegment(new Position(0, 1))),
            CancellationToken.None);

        // Assert: a piece moved
        Assert.Equal(new Position(0, 1), etherealPiece.Position);

        // Assert: score unchanged (no coins collected)
        Assert.Equal(0, game.Scores[p1]);

        // Assert: the game was saved
        await gameRepo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }
}
