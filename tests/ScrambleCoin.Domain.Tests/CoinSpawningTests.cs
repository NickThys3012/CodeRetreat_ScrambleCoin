using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Events;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.Obstacles;
using ScrambleCoin.Domain.Tests.Helpers;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Tests;

public class CoinSpawningTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Lineup NewLineup() =>
        new Lineup(Enumerable.Range(0, 5).Select(i => PieceFactory.Any($"Piece{i}")).ToList());

    /// <summary>
    /// Creates a Game that has been started. After Start() the game is in
    /// TurnPhase.CoinSpawn on turn 1, ready to accept <see cref="Game.SpawnCoins"/>.
    /// </summary>
    private static (Game game, Guid p1, Guid p2) StartedGame()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, NewLineup());
        game.SetLineup(p2, NewLineup());
        game.Start();
        return (game, p1, p2);
    }

    /// <summary>
    /// Creates a Game that has been started with a custom Board (so obstacles can be pre-added).
    /// </summary>
    private static (Game game, Guid p1, Guid p2) StartedGameWithBoard(Board board)
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = new Game(p1, p2, board);
        game.SetLineup(p1, NewLineup());
        game.SetLineup(p2, NewLineup());
        game.Start();
        return (game, p1, p2);
    }

    // ── Phase guard ───────────────────────────────────────────────────────────

    [Fact]
    public void SpawnCoins_OutsideCoinSpawnPhase_ThrowsDomainException()
    {
        var (game, _, _) = StartedGame();
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        Assert.Throws<DomainException>(() =>
            game.SpawnCoins([(new Position(0, 0), CoinType.Silver)]));
    }

    // ── Obstacle guards ───────────────────────────────────────────────────────

    [Fact]
    public void SpawnCoins_OnRockTile_ThrowsDomainException()
    {
        var board = new Board();
        var rockPosition = new Position(3, 3);
        board.AddRock(new Rock(rockPosition));

        var (game, _, _) = StartedGameWithBoard(board);

        Assert.Throws<DomainException>(() =>
            game.SpawnCoins([(rockPosition, CoinType.Silver)]));
    }

    [Fact]
    public void SpawnCoins_OnLakeTile_ThrowsDomainException()
    {
        var board = new Board();
        board.AddLake(new Lake(new Position(2, 2)));
        var lakeCoveredPosition = new Position(2, 2); // top-left of the 2×2 lake

        var (game, _, _) = StartedGameWithBoard(board);

        Assert.Throws<DomainException>(() =>
            game.SpawnCoins([(lakeCoveredPosition, CoinType.Silver)]));
    }

    [Fact]
    public void SpawnCoins_OnLakeTile_InteriorPosition_ThrowsDomainException()
    {
        var board = new Board();
        board.AddLake(new Lake(new Position(2, 2)));
        var interiorLakePosition = new Position(3, 3); // bottom-right of 2×2 lake at (2,2)

        var (game, _, _) = StartedGameWithBoard(board);

        Assert.Throws<DomainException>(() =>
            game.SpawnCoins([(interiorLakePosition, CoinType.Silver)]));
    }

    [Fact]
    public void SpawnCoins_OnOccupiedCoinTile_ThrowsDomainException()
    {
        var board = new Board();
        var coinPosition = new Position(1, 1);
        board.GetTile(coinPosition).SetOccupant(new Coin(CoinType.Silver));

        var (game, _, _) = StartedGameWithBoard(board);

        Assert.Throws<DomainException>(() =>
            game.SpawnCoins([(coinPosition, CoinType.Silver)]));
    }

    [Fact]
    public void SpawnCoins_OnOccupiedPieceTile_ThrowsDomainException()
    {
        var board = new Board();
        var piecePosition = new Position(4, 4);
        board.GetTile(piecePosition).SetOccupant(PieceFactory.Any("Blocker"));

        var (game, _, _) = StartedGameWithBoard(board);

        Assert.Throws<DomainException>(() =>
            game.SpawnCoins([(piecePosition, CoinType.Silver)]));
    }

    // ── Successful spawn ──────────────────────────────────────────────────────

    [Fact]
    public void SpawnCoins_OnFreeTile_PlacesCoin()
    {
        var (game, _, _) = StartedGame();
        var spawnPosition = new Position(5, 5);

        game.SpawnCoins([(spawnPosition, CoinType.Silver)]);

        var tile = game.Board.GetTile(spawnPosition);
        Assert.NotNull(tile.AsCoin);
        Assert.Equal(CoinType.Silver, tile.AsCoin!.CoinType);
    }

    [Fact]
    public void SpawnCoins_OnFreeTile_PlacesGoldCoin()
    {
        var (game, _, _) = StartedGame();
        var spawnPosition = new Position(0, 0);

        game.SpawnCoins([(spawnPosition, CoinType.Gold)]);

        var tile = game.Board.GetTile(spawnPosition);
        Assert.NotNull(tile.AsCoin);
        Assert.Equal(CoinType.Gold, tile.AsCoin!.CoinType);
    }

    // ── CoinsSpawned domain event ─────────────────────────────────────────────

    [Fact]
    public void SpawnCoins_RaisesCoinsSpawnedEvent()
    {
        var (game, _, _) = StartedGame();
        game.ClearDomainEvents();

        game.SpawnCoins([(new Position(0, 0), CoinType.Silver)]);

        var evt = Assert.Single(game.DomainEvents.OfType<CoinsSpawned>());
        Assert.NotNull(evt);
    }

    [Fact]
    public void SpawnCoins_CoinsSpawnedEvent_HasCorrectGameId()
    {
        var (game, _, _) = StartedGame();
        game.ClearDomainEvents();

        game.SpawnCoins([(new Position(0, 0), CoinType.Silver)]);

        var evt = game.DomainEvents.OfType<CoinsSpawned>().Single();
        Assert.Equal(game.Id, evt.GameId);
    }

    [Fact]
    public void SpawnCoins_CoinsSpawnedEvent_HasCorrectTurnNumber()
    {
        var (game, _, _) = StartedGame();
        game.ClearDomainEvents();

        game.SpawnCoins([(new Position(0, 0), CoinType.Silver)]);

        var evt = game.DomainEvents.OfType<CoinsSpawned>().Single();
        Assert.Equal(1, evt.TurnNumber);
    }

    [Fact]
    public void SpawnCoins_CoinsSpawnedEvent_HasCorrectCoinCount()
    {
        var (game, _, _) = StartedGame();
        game.ClearDomainEvents();
        var positions = new[]
        {
            (new Position(0, 0), CoinType.Silver),
            (new Position(1, 1), CoinType.Silver),
            (new Position(2, 2), CoinType.Silver),
        };

        game.SpawnCoins(positions);

        var evt = game.DomainEvents.OfType<CoinsSpawned>().Single();
        Assert.Equal(3, evt.Coins.Count);
    }

    [Fact]
    public void SpawnCoins_CoinsSpawnedEvent_ContainsSpawnedPositions()
    {
        var (game, _, _) = StartedGame();
        game.ClearDomainEvents();
        var spawnPosition = new Position(3, 3);

        game.SpawnCoins([(spawnPosition, CoinType.Silver)]);

        var evt = game.DomainEvents.OfType<CoinsSpawned>().Single();
        Assert.Contains(evt.Coins, c => c.Position == spawnPosition && c.CoinType == CoinType.Silver);
    }

    // ── GetFreeTiles ──────────────────────────────────────────────────────────

    [Fact]
    public void GetFreeTiles_EmptyBoard_Returns64Tiles()
    {
        var board = new Board();

        var freeTiles = board.GetFreeTiles();

        Assert.Equal(64, freeTiles.Count);
    }

    [Fact]
    public void GetFreeTiles_ExcludesRockTiles()
    {
        var board = new Board();
        board.AddRock(new Rock(new Position(0, 0)));

        var freeTiles = board.GetFreeTiles();

        Assert.Equal(63, freeTiles.Count);
    }

    [Fact]
    public void GetFreeTiles_ExcludesRockTile_SpecificPosition()
    {
        var board = new Board();
        var rockPosition = new Position(4, 4);
        board.AddRock(new Rock(rockPosition));

        var freeTiles = board.GetFreeTiles();

        Assert.DoesNotContain(freeTiles, t => t.Position == rockPosition);
    }

    [Fact]
    public void GetFreeTiles_ExcludesLakeTiles()
    {
        var board = new Board();
        board.AddLake(new Lake(new Position(0, 0))); // covers (0,0),(0,1),(1,0),(1,1)

        var freeTiles = board.GetFreeTiles();

        Assert.Equal(60, freeTiles.Count);
    }

    [Fact]
    public void GetFreeTiles_ExcludesAllFourLakeCoveredPositions()
    {
        var board = new Board();
        var topLeft = new Position(2, 2);
        board.AddLake(new Lake(topLeft));

        var freeTiles = board.GetFreeTiles();

        Assert.DoesNotContain(freeTiles, t => t.Position == new Position(2, 2));
        Assert.DoesNotContain(freeTiles, t => t.Position == new Position(2, 3));
        Assert.DoesNotContain(freeTiles, t => t.Position == new Position(3, 2));
        Assert.DoesNotContain(freeTiles, t => t.Position == new Position(3, 3));
    }

    [Fact]
    public void GetFreeTiles_ExcludesOccupiedTiles()
    {
        var board = new Board();
        board.GetTile(new Position(0, 0)).SetOccupant(new Coin(CoinType.Silver));

        var freeTiles = board.GetFreeTiles();

        Assert.Equal(63, freeTiles.Count);
    }

    [Fact]
    public void GetFreeTiles_ExcludesOccupiedTile_SpecificPosition()
    {
        var board = new Board();
        var coinPosition = new Position(3, 3);
        board.GetTile(coinPosition).SetOccupant(new Coin(CoinType.Silver));

        var freeTiles = board.GetFreeTiles();

        Assert.DoesNotContain(freeTiles, t => t.Position == coinPosition);
    }

    [Fact]
    public void GetFreeTiles_ExcludesPieceTile()
    {
        var board = new Board();
        var piecePosition = new Position(5, 5);
        board.GetTile(piecePosition).SetOccupant(PieceFactory.Any("TestPiece"));

        var freeTiles = board.GetFreeTiles();

        Assert.Equal(63, freeTiles.Count);
    }

    [Fact]
    public void GetFreeTiles_WithMultipleObstaclesAndOccupants_ReturnsCorrectCount()
    {
        var board = new Board();
        board.AddRock(new Rock(new Position(0, 0)));     // -1
        board.AddLake(new Lake(new Position(2, 2)));     // -4
        board.GetTile(new Position(7, 7)).SetOccupant(new Coin(CoinType.Gold)); // -1

        var freeTiles = board.GetFreeTiles();

        Assert.Equal(58, freeTiles.Count);
    }
}
