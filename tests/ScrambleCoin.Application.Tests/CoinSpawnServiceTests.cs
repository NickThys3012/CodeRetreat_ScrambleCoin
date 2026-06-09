using Microsoft.Extensions.Logging;
using NSubstitute;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Services;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Unit tests for <see cref="CoinSpawnService"/> (Issue #36).
/// Covers all 5 turn schedules, the tile-shortage edge case, phase advancement, and persistence.
/// </summary>
public class CoinSpawnServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a game in the CoinSpawn phase for <paramref name="targetTurn"/>.
    /// For turn 1: just calls Start(). For turns 2–5: advances through
    /// (targetTurn - 1) complete turns without placing any pieces.
    /// </summary>
    private static Game GameAtTurnCoinSpawnPhase(int targetTurn)
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var p1Pieces = Enumerable.Range(0, 5)
            .Select(i => new Piece(Guid.NewGuid(), $"P1Piece{i}", p1,
                EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();
        var p2Pieces = Enumerable.Range(0, 5)
            .Select(i => new Piece(Guid.NewGuid(), $"P2Piece{i}", p2,
                EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(p1Pieces));
        game.SetLineup(p2, new Lineup(p2Pieces));
        game.Start(); // → CoinSpawn, turn 1

        // Advance through (targetTurn - 1) full turns with one piece per player on the board
        // so MovePhase is not immediately auto-skipped (0-piece auto-skip behaviour).
        // Place pieces on turn 1; skip placement on subsequent turns (pieces stay on board).
        for (var t = 1; t < targetTurn; t++)
        {
            game.AdvancePhase(); // CoinSpawn → PlacePhase
            if (t == 1)
            {
                game.PlacePiece(p1, p1Pieces[0].Id, new Position(0, 0)); // places + marks p1 done
                game.PlacePiece(p2, p2Pieces[0].Id, new Position(7, 7)); // marks p2 done → auto-advances to MovePhase
            }
            else
            {
                game.SkipPlacement(p1);
                game.SkipPlacement(p2); // auto-advances to MovePhase (pieces already on board)
            }
            game.AdvanceTurn(); // MovePhase → CoinSpawn (next turn)
        }

        return game;
    }

    private static CoinSpawnService BuildService(IGameRepository repo, Random? random = null) =>
        new(
            repo,
            random ?? new Random(42),
            Substitute.For<ILogger<CoinSpawnService>>());

    // ── Turn 1–3: Silver coins ────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Turn1_SpawnsSilverCoins()
    {
        // Arrange
        var game = GameAtTurnCoinSpawnPhase(1);
        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        var service = BuildService(repo, new Random(0));

        // Act
        await service.ExecuteAsync(game.Id);

        // Assert: every spawned coin on turn 1 is Silver
        var coinTiles = game.Board.GetAllCoins();
        Assert.NotEmpty(coinTiles);
        Assert.All(coinTiles, t => Assert.Equal(CoinType.Silver, t.AsCoin!.CoinType));
    }

    [Fact]
    public async Task ExecuteAsync_Turn2_SpawnsSilverCoins()
    {
        // Arrange
        var game = GameAtTurnCoinSpawnPhase(2);
        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        var service = BuildService(repo, new Random(0));

        // Act
        await service.ExecuteAsync(game.Id);

        // Assert: every spawned coin on turn 2 is Silver
        var coinTiles = game.Board.GetAllCoins();
        Assert.NotEmpty(coinTiles);
        Assert.All(coinTiles, t => Assert.Equal(CoinType.Silver, t.AsCoin!.CoinType));
    }

    [Fact]
    public async Task ExecuteAsync_Turn3_SpawnsSilverCoins()
    {
        // Arrange
        var game = GameAtTurnCoinSpawnPhase(3);
        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        var service = BuildService(repo, new Random(0));

        // Act
        await service.ExecuteAsync(game.Id);

        // Assert: every spawned coin on turn 3 is Silver
        var coinTiles = game.Board.GetAllCoins();
        Assert.NotEmpty(coinTiles);
        Assert.All(coinTiles, t => Assert.Equal(CoinType.Silver, t.AsCoin!.CoinType));
    }

    // ── Turn 4–5: Gold coins ──────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Turn4_SpawnsGoldCoins()
    {
        // Arrange
        var game = GameAtTurnCoinSpawnPhase(4);
        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        var service = BuildService(repo, new Random(0));

        // Act
        await service.ExecuteAsync(game.Id);

        // Assert: all 4 coins spawned on turn 4 are Gold
        var coinTiles = game.Board.GetAllCoins();
        Assert.Equal(4, coinTiles.Count);
        Assert.All(coinTiles, t => Assert.Equal(CoinType.Gold, t.AsCoin!.CoinType));
    }

    [Fact]
    public async Task ExecuteAsync_Turn5_SpawnsGoldCoins()
    {
        // Arrange
        var game = GameAtTurnCoinSpawnPhase(5);
        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        var service = BuildService(repo, new Random(0));

        // Act
        await service.ExecuteAsync(game.Id);

        // Assert: all 3 coins spawned on turn 5 are Gold
        var coinTiles = game.Board.GetAllCoins();
        Assert.Equal(3, coinTiles.Count);
        Assert.All(coinTiles, t => Assert.Equal(CoinType.Gold, t.AsCoin!.CoinType));
    }

    // ── Tile-shortage edge case ───────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenFewerFreeTilesThanScheduled_SpawnsOnlyAsManyAsFit()
    {
        // Arrange: start at turn 1 CoinSpawn, then manually fill the board, leaving only 2 free tiles.
        // Turn 1 schedule spawns 7–9 coins, but only 2 tiles are free — no exception should be thrown.
        var game = GameAtTurnCoinSpawnPhase(1);

        // Occupy all tiles except (7,6) and (7,7)
        for (var row = 0; row < 8; row++)
        for (var col = 0; col < 8; col++)
        {
            if (row == 7 && col >= 6) continue; // leave 2 free
            game.Board.GetTile(new Position(row, col)).SetOccupant(new Coin(CoinType.Silver));
        }

        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        var service = BuildService(repo, new Random(1));

        // Act — must not throw even though fewer free tiles than scheduled coins
        var exception = await Record.ExceptionAsync(() => service.ExecuteAsync(game.Id));
        Assert.Null(exception);

        // Assert: at most 2 coins placed in the originally free tiles
        var coinAtFree1 = game.Board.GetTile(new Position(7, 6)).AsCoin;
        var coinAtFree2 = game.Board.GetTile(new Position(7, 7)).AsCoin;
        var coinsPlacedInFreeTiles = new[] { coinAtFree1, coinAtFree2 }.Count(c => c is not null);
        Assert.Equal(2, coinsPlacedInFreeTiles);
    }

    // ── Phase advancement ─────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_AdvancesPhaseFromCoinSpawnToPlacePhase()
    {
        // Arrange
        var game = GameAtTurnCoinSpawnPhase(1);
        Assert.Equal(TurnPhase.CoinSpawn, game.CurrentPhase); // precondition

        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        var service = BuildService(repo);

        // Act
        await service.ExecuteAsync(game.Id);

        // Assert: phase advanced to PlacePhase
        Assert.Equal(TurnPhase.PlacePhase, game.CurrentPhase);
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_SavesGame()
    {
        // Arrange
        var game = GameAtTurnCoinSpawnPhase(1);
        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        var service = BuildService(repo);

        // Act
        await service.ExecuteAsync(game.Id);

        // Assert: SaveAsync called exactly once
        await repo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }
}
