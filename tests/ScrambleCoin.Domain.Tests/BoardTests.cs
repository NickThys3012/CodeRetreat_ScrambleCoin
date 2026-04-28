using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.Obstacles;
using ScrambleCoin.Domain.Tests.Helpers;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Tests;

public class BoardTests
{
    // ── Construction ──────────────────────────────────────────────────────────

    [Fact]
    public void Board_Constructor_ShouldCreateExactly64Tiles_AllEmpty()
    {
        var board = new Board();
        var count = 0;
        for (var row = 0; row < 8; row++)
        for (var col = 0; col < 8; col++)
        {
            var tile = board.GetTile(new Position(row, col));
            Assert.NotNull(tile);
            Assert.True(tile.IsEmpty, $"Tile at ({row},{col}) should be empty after construction.");
            count++;
        }
        Assert.Equal(64, count);
    }

    [Fact]
    public void GetTile_At0_0_ShouldReturnTileAtCorrectPosition()
    {
        var board = new Board();
        var tile = board.GetTile(new Position(0, 0));
        Assert.Equal(new Position(0, 0), tile.Position);
    }

    [Fact]
    public void GetTile_At7_7_ShouldReturnTileAtCorrectPosition()
    {
        var board = new Board();
        var tile = board.GetTile(new Position(7, 7));
        Assert.Equal(new Position(7, 7), tile.Position);
    }

    [Fact]
    public void GetTile_AllPositions_ShouldReturnDistinctTiles()
    {
        var board = new Board();
        var tiles = new HashSet<Tile>();
        for (var row = 0; row < 8; row++)
        for (var col = 0; col < 8; col++)
            tiles.Add(board.GetTile(new Position(row, col)));
        Assert.Equal(64, tiles.Count);
    }

    // ── Rock passability ──────────────────────────────────────────────────────

    [Fact]
    public void IsPassable_WithRockAtDestination_ShouldReturnFalse()
    {
        var board = new Board();
        var rockPos = new Position(3, 4);
        board.AddRock(new Rock(rockPos));
        var from = new Position(3, 3);
        Assert.False(board.IsPassable(from, rockPos));
    }

    [Fact]
    public void IsPassable_WithRockElsewhere_ShouldNotAffectOtherTiles()
    {
        var board = new Board();
        board.AddRock(new Rock(new Position(5, 5)));
        var from = new Position(3, 3);
        var to = new Position(3, 4);
        Assert.True(board.IsPassable(from, to));
    }

    [Fact]
    public void IsPassable_WithRockAtSource_ButNotDestination_ShouldReturnTrue()
    {
        var board = new Board();
        var from = new Position(3, 3);
        board.AddRock(new Rock(from));
        var to = new Position(3, 4);
        // Rock at source doesn't block; only at destination
        Assert.True(board.IsPassable(from, to));
    }

    // ── Lake passability ──────────────────────────────────────────────────────

    [Fact]
    public void IsPassable_ToTopLeftOfLake_ShouldReturnFalse()
    {
        var board = new Board();
        board.AddLake(new Lake(new Position(2, 2)));
        var from = new Position(2, 1);
        Assert.False(board.IsPassable(from, new Position(2, 2)));
    }

    [Fact]
    public void IsPassable_ToTopRightOfLake_ShouldReturnFalse()
    {
        var board = new Board();
        board.AddLake(new Lake(new Position(2, 2)));
        var from = new Position(2, 4); // approach from right
        Assert.False(board.IsPassable(from, new Position(2, 3)));
    }

    [Fact]
    public void IsPassable_ToBottomLeftOfLake_ShouldReturnFalse()
    {
        var board = new Board();
        board.AddLake(new Lake(new Position(2, 2)));
        var from = new Position(4, 2); // approach from below
        Assert.False(board.IsPassable(from, new Position(3, 2)));
    }

    [Fact]
    public void IsPassable_ToBottomRightOfLake_ShouldReturnFalse()
    {
        var board = new Board();
        board.AddLake(new Lake(new Position(2, 2)));
        var from = new Position(4, 3); // approach from below
        Assert.False(board.IsPassable(from, new Position(3, 3)));
    }

    // ── Fence passability (orthogonal) ────────────────────────────────────────

    [Fact]
    public void IsPassable_WithFenceOnEdge_ShouldReturnFalse()
    {
        var board = new Board();
        var a = new Position(2, 2);
        var b = new Position(2, 3);
        board.AddFence(new Fence(a, b));
        Assert.False(board.IsPassable(a, b));
    }

    [Fact]
    public void IsPassable_WithFenceOnEdge_ReverseDirection_ShouldReturnFalse()
    {
        var board = new Board();
        var a = new Position(2, 2);
        var b = new Position(2, 3);
        board.AddFence(new Fence(a, b));
        Assert.False(board.IsPassable(b, a));
    }

    [Fact]
    public void IsPassable_WithFenceOnDifferentEdge_ShouldReturnTrue()
    {
        var board = new Board();
        var a = new Position(2, 2);
        var b = new Position(2, 3);
        board.AddFence(new Fence(a, b));
        // Move on a different edge
        var c = new Position(3, 2);
        var d = new Position(3, 3);
        Assert.True(board.IsPassable(c, d));
    }

    // ── Non-adjacent throws ───────────────────────────────────────────────────

    [Fact]
    public void IsPassable_WithNonAdjacentPositions_ShouldThrowDomainException()
    {
        var board = new Board();
        var a = new Position(0, 0);
        var c = new Position(0, 2);
        Assert.Throws<DomainException>(() => board.IsPassable(a, c));
    }

    [Fact]
    public void IsPassable_WithSamePosition_ShouldThrowDomainException()
    {
        var board = new Board();
        var a = new Position(3, 3);
        Assert.Throws<DomainException>(() => board.IsPassable(a, a));
    }

    [Fact]
    public void IsPassable_TwoRowsApart_ShouldThrowDomainException()
    {
        var board = new Board();
        var a = new Position(1, 1);
        var b = new Position(3, 1);
        Assert.Throws<DomainException>(() => board.IsPassable(a, b));
    }

    // ── Fence diagonal blocking ───────────────────────────────────────────────

    [Fact]
    public void IsPassable_DiagonalWithBothCornerFencesAtFromSide_ShouldReturnFalse()
    {
        // from=(2,2), to=(3,3), cornerA=(3,2), cornerB=(2,3)
        // Block corner at 'from': fence from↔cornerA AND fence from↔cornerB
        var board = new Board();
        var from = new Position(2, 2);
        var cornerA = new Position(3, 2); // rowDiff=+1, colDiff=0
        var cornerB = new Position(2, 3); // rowDiff=0,  colDiff=+1
        board.AddFence(new Fence(from, cornerA));
        board.AddFence(new Fence(from, cornerB));
        var to = new Position(3, 3);
        Assert.False(board.IsPassable(from, to));
    }

    [Fact]
    public void IsPassable_DiagonalWithBothCornerFencesAtToSide_ShouldReturnFalse()
    {
        // from=(2,2), to=(3,3), cornerA=(3,2), cornerB=(2,3)
        // Block corner at 'to': fence to↔cornerA AND fence to↔cornerB
        var board = new Board();
        var from = new Position(2, 2);
        var to = new Position(3, 3);
        var cornerA = new Position(3, 2);
        var cornerB = new Position(2, 3);
        board.AddFence(new Fence(to, cornerA));
        board.AddFence(new Fence(to, cornerB));
        Assert.False(board.IsPassable(from, to));
    }

    [Fact]
    public void IsPassable_DiagonalWithOnlyOneCornerFenceAtFromSide_ShouldReturnTrue()
    {
        var board = new Board();
        var from = new Position(2, 2);
        var cornerA = new Position(3, 2);
        board.AddFence(new Fence(from, cornerA)); // only one fence
        var to = new Position(3, 3);
        Assert.True(board.IsPassable(from, to));
    }

    [Fact]
    public void IsPassable_DiagonalWithNoFences_ShouldReturnTrue()
    {
        var board = new Board();
        var from = new Position(2, 2);
        var to = new Position(3, 3);
        Assert.True(board.IsPassable(from, to));
    }

    [Fact]
    public void IsPassable_DiagonalWithOnlyOneCornerFenceAtToSide_ShouldReturnTrue()
    {
        var board = new Board();
        var from = new Position(2, 2);
        var to = new Position(3, 3);
        var cornerA = new Position(3, 2);
        board.AddFence(new Fence(to, cornerA)); // only one fence at 'to' side
        Assert.True(board.IsPassable(from, to));
    }

    // ── GetAllCoins ───────────────────────────────────────────────────────────

    [Fact]
    public void GetAllCoins_EmptyBoard_ShouldReturnEmptyList()
    {
        var board = new Board();
        Assert.Empty(board.GetAllCoins());
    }

    [Fact]
    public void GetAllCoins_WithOneCoin_ShouldReturnOneTile()
    {
        var board = new Board();
        var tile = board.GetTile(new Position(1, 1));
        tile.SetOccupant(new Coin(CoinType.Silver));
        Assert.Single(board.GetAllCoins());
    }

    [Fact]
    public void GetAllCoins_WithPieceOnly_ShouldReturnEmptyList()
    {
        var board = new Board();
        var tile = board.GetTile(new Position(1, 1));
        tile.SetOccupant(PieceFactory.Any("Alice"));
        Assert.Empty(board.GetAllCoins());
    }

    [Fact]
    public void GetAllCoins_WithCoinAndPiece_ShouldReturnOnlyTilesWithCoins()
    {
        var board = new Board();
        board.GetTile(new Position(0, 0)).SetOccupant(new Coin(CoinType.Gold));
        board.GetTile(new Position(1, 1)).SetOccupant(PieceFactory.Any("Bob"));
        var coins = board.GetAllCoins();
        Assert.Single(coins);
        Assert.NotNull(coins[0].AsCoin);
    }

    [Fact]
    public void GetAllCoins_WithMultipleCoins_ShouldReturnAll()
    {
        var board = new Board();
        board.GetTile(new Position(0, 0)).SetOccupant(new Coin(CoinType.Silver));
        board.GetTile(new Position(1, 1)).SetOccupant(new Coin(CoinType.Gold));
        board.GetTile(new Position(2, 2)).SetOccupant(new Coin(CoinType.Silver));
        Assert.Equal(3, board.GetAllCoins().Count);
    }

    // ── GetAllOccupiedTiles ───────────────────────────────────────────────────

    [Fact]
    public void GetAllOccupiedTiles_EmptyBoard_ShouldReturnEmptyList()
    {
        var board = new Board();
        Assert.Empty(board.GetAllOccupiedTiles());
    }

    [Fact]
    public void GetAllOccupiedTiles_WithCoin_ShouldReturnTile()
    {
        var board = new Board();
        board.GetTile(new Position(0, 0)).SetOccupant(new Coin(CoinType.Silver));
        Assert.Single(board.GetAllOccupiedTiles());
    }

    [Fact]
    public void GetAllOccupiedTiles_WithPiece_ShouldReturnTile()
    {
        var board = new Board();
        board.GetTile(new Position(0, 0)).SetOccupant(PieceFactory.Any("Alice"));
        Assert.Single(board.GetAllOccupiedTiles());
    }

    [Fact]
    public void GetAllOccupiedTiles_WithCoinsAndPieces_ShouldReturnAll()
    {
        var board = new Board();
        board.GetTile(new Position(0, 0)).SetOccupant(new Coin(CoinType.Silver));
        board.GetTile(new Position(1, 1)).SetOccupant(PieceFactory.Any("Alice"));
        board.GetTile(new Position(2, 2)).SetOccupant(new Coin(CoinType.Gold));
        Assert.Equal(3, board.GetAllOccupiedTiles().Count);
    }

    // ── GetAllObstacles ───────────────────────────────────────────────────────

    [Fact]
    public void GetAllObstacles_EmptyBoard_ShouldReturnEmptyCollections()
    {
        var board = new Board();
        var obstacles = board.GetAllObstacles();
        Assert.Empty(obstacles.Rocks);
        Assert.Empty(obstacles.Lakes);
        Assert.Empty(obstacles.Fences);
    }

    [Fact]
    public void GetAllObstacles_AfterAddingRock_ShouldReturnRock()
    {
        var board = new Board();
        var rock = new Rock(new Position(1, 1));
        board.AddRock(rock);
        var obstacles = board.GetAllObstacles();
        Assert.Single(obstacles.Rocks);
        Assert.Contains(rock, obstacles.Rocks);
    }

    [Fact]
    public void GetAllObstacles_AfterAddingLake_ShouldReturnLake()
    {
        var board = new Board();
        var lake = new Lake(new Position(1, 1));
        board.AddLake(lake);
        var obstacles = board.GetAllObstacles();
        Assert.Single(obstacles.Lakes);
        Assert.Contains(lake, obstacles.Lakes);
    }

    [Fact]
    public void GetAllObstacles_AfterAddingFence_ShouldReturnFence()
    {
        var board = new Board();
        var fence = new Fence(new Position(1, 1), new Position(1, 2));
        board.AddFence(fence);
        var obstacles = board.GetAllObstacles();
        Assert.Single(obstacles.Fences);
        Assert.Contains(fence, obstacles.Fences);
    }

    [Fact]
    public void GetAllObstacles_AfterAddingAllTypes_ShouldReturnAll()
    {
        var board = new Board();
        board.AddRock(new Rock(new Position(0, 0)));
        board.AddRock(new Rock(new Position(1, 0)));
        board.AddLake(new Lake(new Position(4, 4)));
        board.AddFence(new Fence(new Position(2, 2), new Position(2, 3)));
        board.AddFence(new Fence(new Position(3, 3), new Position(4, 3)));
        var obstacles = board.GetAllObstacles();
        Assert.Equal(2, obstacles.Rocks.Count);
        Assert.Single(obstacles.Lakes);
        Assert.Equal(2, obstacles.Fences.Count);
    }
}
