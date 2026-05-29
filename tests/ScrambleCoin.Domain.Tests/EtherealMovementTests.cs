using System.Collections.ObjectModel;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Events;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.Obstacles;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Tests;

/// <summary>
/// Unit tests for an Ethereal movement type (Issue #46).
/// Ethereal pieces pass through obstacles and pieces on intermediate tiles but must end on a free tile.
/// Coins are collected on all tiles in the path (intermediate and destination).
/// Fences still block Ethereal movement.
/// </summary>
public class EtherealMovementTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a game in MovePhase with exactly one Ethereal piece for P1 and a normal piece for P2.
    /// P1's Ethereal piece is at p1StartPos; P2's orthogonal piece is at p2StartPos.
    /// </summary>
    private static (Game game, Guid p1, Guid p2, Piece p1Piece, Piece p2Piece) GameInMovePhaseWithEtherealPiece(
        int p1MaxDistance = 3,
        Position? p1StartPos = null,
        Position? p2StartPos = null)
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var board = new Board();

        var actualP1Pos = p1StartPos ?? new Position(0, 0);
        var actualP2Pos = p2StartPos ?? new Position(7, 7);

        // Create an Ethereal piece for P1 (like a fairy/ghost)
        var p1Piece = new Piece(
            Guid.NewGuid(), "TestEthereal", p1,
            EntryPointType.Corners, MovementType.Ethereal, p1MaxDistance, 1);
        var p1Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(
                Guid.NewGuid(), $"P1Fill{i}", p1, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var p2Piece = new Piece(
            Guid.NewGuid(), "P2Mover", p2,
            EntryPointType.Borders, MovementType.Orthogonal, 1, 1);
        var p2Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(
                Guid.NewGuid(), $"P2Fill{i}", p2, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var game = new Game(p1, p2, board);
        game.SetLineup(p1, new Lineup(new[] { p1Piece }.Concat(p1Fill)));
        game.SetLineup(p2, new Lineup(new[] { p2Piece }.Concat(p2Fill)));
        game.Start();
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        // Placing both pieces auto-advances to MovePhase.
        game.PlacePiece(p1, p1Piece.Id, actualP1Pos);
        game.PlacePiece(p2, p2Piece.Id, actualP2Pos);

        return (game, p1, p2, p1Piece, p2Piece);
    }

    /// <summary>
    /// Builds a multistep segment for Ethereal movement.
    /// </summary>
    private static ReadOnlyCollection<IReadOnlyList<Position>> BuildEtherealSegment(params Position[] positions)
    {
        IReadOnlyList<Position> segment = positions.ToList().AsReadOnly();
        return new List<IReadOnlyList<Position>> { segment }.AsReadOnly();
    }

    // ── Core Ethereal Logic Tests ──────────────────────────────────────────

    [Fact]
    public void Ethereal_PassThroughRock_CollectsCoinBehindRock()
    {
        // Arrange: Ethereal piece at (0,0), rock at (0,1), coin at (0,2)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithEtherealPiece(
            p1MaxDistance: 2,
            p1StartPos: new Position(0, 0));
        
        var rockPos = new Position(0, 1);
        var coinPos = new Position(0, 2);
        
        game.Board.AddRock(new Rock(rockPos));
        game.Board.GetTile(coinPos).SetOccupant(new Coin(CoinType.Silver));

        // Act: Move ethereal through rock to coin tile
        game.MovePiece(p1, p1Piece.Id, BuildEtherealSegment(rockPos, coinPos));

        // Assert: a piece moved to coin position
        Assert.Equal(coinPos, p1Piece.Position);

        // Assert: coin was collected
        Assert.Equal(1, game.Scores[p1]);

        // Assert: coin removed from board
        Assert.Null(game.Board.GetTile(coinPos).AsCoin);
    }

    [Fact]
    public void Ethereal_PassThroughOpponentPiece_ReachesDestination()
    {
        // Arrange: Ethereal piece at (0,0), opponent piece at (0,1), destination at (0,2)
        var (game, p1, p2, p1Piece, p2Piece) = GameInMovePhaseWithEtherealPiece(
            p1MaxDistance: 2,
            p1StartPos: new Position(0, 0),
            p2StartPos: new Position(0, 1));

        // Act: Move ethereal through an opponent piece
        game.MovePiece(p1, p1Piece.Id, BuildEtherealSegment(
            new Position(0, 1), // through opponent
            new Position(0, 2)  // to free tile
        ));

        // Assert: piece moved to destination (passed through opponent)
        Assert.Equal(new Position(0, 2), p1Piece.Position);

        // Assert: an opponent piece is still there (not captured)
        Assert.Equal(new Position(0, 1), p2Piece.Position);
    }

    [Fact]
    public void Ethereal_EndOnCoinTile_CollectsCoin()
    {
        // Arrange: Ethereal piece at (0,0), coin at destination (0,1)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithEtherealPiece(
            p1MaxDistance: 1,
            p1StartPos: new Position(0, 0));
        
        var destPos = new Position(0, 1);
        game.Board.GetTile(destPos).SetOccupant(new Coin(CoinType.Gold));

        // Act: Move ethereal to coin tile
        game.MovePiece(p1, p1Piece.Id, BuildEtherealSegment(destPos));

        // Assert: coin collected
        Assert.Equal(3, game.Scores[p1]); // Gold = 3 pts

        // Assert: coin removed
        Assert.Null(game.Board.GetTile(destPos).AsCoin);
    }

    [Fact]
    public void Ethereal_EndOnPieceTile_Throws()
    {
        // Arrange: Ethereal piece at (0,0), opponent piece at (0,1)
        var (game, p1, p2, p1Piece, _) = GameInMovePhaseWithEtherealPiece(
            p1MaxDistance: 1,
            p1StartPos: new Position(0, 0),
            p2StartPos: new Position(0, 1));

        // Act & Assert: attempting to end on occupied tile throws
        var ex = Assert.Throws<DomainException>(() =>
            game.MovePiece(p1, p1Piece.Id, BuildEtherealSegment(new Position(0, 1)))
        );
        Assert.Contains("Ethereal movement must end on a free tile", ex.Message);
    }

    [Fact]
    public void Ethereal_InvalidDestination_DoesNotCollectIntermediateCoins()
    {
        // Arrange: coin on the first step, but destination is occupied by an opponent.
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithEtherealPiece(
            p1MaxDistance: 2,
            p1StartPos: new Position(0, 0),
            p2StartPos: new Position(0, 2));

        var coinPos = new Position(0, 1);
        game.Board.GetTile(coinPos).SetOccupant(new Coin(CoinType.Gold));

        // Act & Assert: validation fails before any movement side effects are applied.
        var ex = Assert.Throws<DomainException>(() =>
            game.MovePiece(p1, p1Piece.Id, BuildEtherealSegment(coinPos, new Position(0, 2)))
        );
        Assert.Contains("Ethereal movement must end on a free tile", ex.Message);

        Assert.Equal(new Position(0, 0), p1Piece.Position);
        Assert.Equal(0, game.Scores[p1]);
        Assert.NotNull(game.Board.GetTile(coinPos).AsCoin);
        Assert.Empty(game.DomainEvents.OfType<CoinCollected>());
    }

    [Fact]
    public void Ethereal_FenceStillBlocks_Throws()
    {
        // Arrange: Ethereal piece at (0,0), fence between (0,0) and (0,1)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithEtherealPiece(
            p1MaxDistance: 1,
            p1StartPos: new Position(0, 0));
        
        var fence = new Fence(new Position(0, 0), new Position(0, 1)); // Vertical fence
        game.Board.AddFence(fence);

        // Act & Assert: attempting to cross fence throws
        var ex = Assert.Throws<DomainException>(() =>
            game.MovePiece(p1, p1Piece.Id, BuildEtherealSegment(new Position(0, 1)))
        );
        Assert.Contains("blocked by a fence", ex.Message);
    }

    [Fact]
    public void Ethereal_CollectCoinsOnAllTilesInPath()
    {
        // Arrange: Ethereal at (0,0), path through (0,1), (0,2), (0,3)
        //         Coins at intermediate (0,1) and (0,2)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithEtherealPiece(
            p1MaxDistance: 3,
            p1StartPos: new Position(0, 0));
        
        game.Board.GetTile(new Position(0, 1)).SetOccupant(new Coin(CoinType.Silver));
        game.Board.GetTile(new Position(0, 2)).SetOccupant(new Coin(CoinType.Silver));
        game.Board.GetTile(new Position(0, 3)).SetOccupant(new Coin(CoinType.Gold));

        // Act: Move through all tiles
        game.MovePiece(p1, p1Piece.Id, BuildEtherealSegment(
            new Position(0, 1),
            new Position(0, 2),
            new Position(0, 3)
        ));

        // Assert: all coins collected (1 + 1 + 3 = 5)
        Assert.Equal(5, game.Scores[p1]);
    }

    [Fact]
    public void Ethereal_RaisesCoinsCollectedEvents()
    {
        // Arrange: Ethereal at (0,0), path with coins at (0,1) and (0,2)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithEtherealPiece(
            p1MaxDistance: 2,
            p1StartPos: new Position(0, 0));
        
        game.Board.GetTile(new Position(0, 1)).SetOccupant(new Coin(CoinType.Silver));
        game.Board.GetTile(new Position(0, 2)).SetOccupant(new Coin(CoinType.Gold));

        // Act: Move through all tiles
        game.MovePiece(p1, p1Piece.Id, BuildEtherealSegment(
            new Position(0, 1),
            new Position(0, 2)
        ));

        // Assert: CoinCollected events raised
        var coinEvents = game.DomainEvents.OfType<CoinCollected>().ToList();
        Assert.Equal(2, coinEvents.Count);
        Assert.Contains(coinEvents, e => e.Position == new Position(0, 1) && e.CoinType == CoinType.Silver);
        Assert.Contains(coinEvents, e => e.Position == new Position(0, 2) && e.CoinType == CoinType.Gold);
    }

    [Fact]
    public void Ethereal_CannotExceedMaxDistance()
    {
        // Arrange: Ethereal with MaxDistance=2
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithEtherealPiece(
            p1MaxDistance: 2,
            p1StartPos: new Position(0, 0));

        // Act & Assert: attempting 3-step movement throws
        var ex = Assert.Throws<DomainException>(() =>
            game.MovePiece(p1, p1Piece.Id, BuildEtherealSegment(
                new Position(0, 1),
                new Position(0, 2),
                new Position(0, 3)
            ))
        );
        Assert.Contains("MaxDistance", ex.Message);
    }

    [Fact]
    public void Ethereal_DiagonalMovement_Allowed()
    {
        // Arrange: Ethereal at (0,0), move diagonally to (2,2)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithEtherealPiece(
            p1MaxDistance: 2,
            p1StartPos: new Position(0, 0));
        
        game.Board.GetTile(new Position(1, 1)).SetOccupant(new Coin(CoinType.Silver));

        // Act: Move diagonally through coin to destination
        game.MovePiece(p1, p1Piece.Id, BuildEtherealSegment(
            new Position(1, 1),
            new Position(2, 2)
        ));

        // Assert: moved to destination and collected coin
        Assert.Equal(new Position(2, 2), p1Piece.Position);
        Assert.Equal(1, game.Scores[p1]);
    }

    [Fact]
    public void Ethereal_NonAdjacentStep_Throws()
    {
        // Arrange: Ethereal at (0,0)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithEtherealPiece(
            p1MaxDistance: 3,
            p1StartPos: new Position(0, 0));

        // Act & Assert: non-adjacent step throws
        var ex = Assert.Throws<DomainException>(() =>
            game.MovePiece(p1, p1Piece.Id, BuildEtherealSegment(
                new Position(0, 2) // Not adjacent!
            ))
        );
        Assert.Contains("not adjacent", ex.Message);
    }

    [Fact]
    public void Ethereal_MultipleRocksAndPieces_PassesThrough()
    {
        // Arrange: Ethereal at (0,0), path with rock at (0,1), opponent at (0,2), rock at (0,3), destination (0,4)
        var (game, p1, p2, p1Piece, p2Piece) = GameInMovePhaseWithEtherealPiece(
            p1MaxDistance: 4,
            p1StartPos: new Position(0, 0),
            p2StartPos: new Position(0, 2)); // Opponent at (0,2)

        game.Board.AddRock(new Rock(new Position(0, 1))); // Rock at (0,1)
        game.Board.AddRock(new Rock(new Position(0, 3))); // Rock at (0,3)
        game.Board.GetTile(new Position(0, 4)).SetOccupant(new Coin(CoinType.Silver));

        // Act: Move through obstacles and opponent
        game.MovePiece(p1, p1Piece.Id, BuildEtherealSegment(
            new Position(0, 1),
            new Position(0, 2),
            new Position(0, 3),
            new Position(0, 4)
        ));

        // Assert: reached destination and collected a coin
        Assert.Equal(new Position(0, 4), p1Piece.Position);
        Assert.Equal(1, game.Scores[p1]);
        
        // Assert: an opponent piece still in place
        Assert.Equal(new Position(0, 2), p2Piece.Position);
    }

    [Fact]
    public void Ethereal_CornerFence_StillBlocks()
    {
        // Arrange: Ethereal at (0,0), diagonal move blocked by corner fences
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithEtherealPiece(
            p1MaxDistance: 1,
            p1StartPos: new Position(0, 0));
        
        // Add two fences forming an L-shape (corner at (1,1))
        game.Board.AddFence(new Fence(new Position(0, 0), new Position(1, 0))); // Vertical fence
        game.Board.AddFence(new Fence(new Position(0, 0), new Position(0, 1))); // Horizontal fence

        // Act & Assert: diagonal move blocked by corner fence
        var ex = Assert.Throws<DomainException>(() =>
            game.MovePiece(p1, p1Piece.Id, BuildEtherealSegment(new Position(1, 1)))
        );
        Assert.Contains("blocked by a fence", ex.Message);
    }

    [Fact]
    public void Ethereal_EmptySegmentWhenFenceBlocked()
    {
        // Arrange: Ethereal piece at (0,0) with all adjacent tiles blocked by fences
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithEtherealPiece(
            p1MaxDistance: 1,
            p1StartPos: new Position(0, 0));
        
        // Add fences on all edges around (0,0)
        game.Board.AddFence(new Fence(new Position(0, 0), new Position(0, 1))); // Right
        game.Board.AddFence(new Fence(new Position(0, 0), new Position(1, 0))); // Down

        // Act: Submit an empty segment
        game.MovePiece(p1, p1Piece.Id, new List<IReadOnlyList<Position>>
        {
            new List<Position>().AsReadOnly()
        }.AsReadOnly());

        // Assert: a piece did not move
        Assert.Equal(new Position(0, 0), p1Piece.Position);
    }

    [Fact]
    public void Ethereal_PieceMoved_EventRaisedWithFullPath()
    {
        // Arrange: Ethereal path (0,0) → (0,1) → (0,2)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithEtherealPiece(
            p1MaxDistance: 2,
            p1StartPos: new Position(0, 0));

        // Act: Move ethereal
        game.MovePiece(p1, p1Piece.Id, BuildEtherealSegment(
            new Position(0, 1),
            new Position(0, 2)
        ));

        // Assert: PieceMoved event includes a full path
        var moveEvent = game.DomainEvents.OfType<PieceMoved>().Single();
        Assert.Equal(new Position(0, 0), moveEvent.From);
        Assert.Equal(new Position(0, 2), moveEvent.To);
        Assert.Equal([new Position(0, 1), new Position(0, 2)], moveEvent.Path);
    }

    [Fact]
    public void Ethereal_EndOnRock_Throws()
    {
        // Arrange: Ethereal piece at (0,0), rock at destination (0,1)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithEtherealPiece(
            p1MaxDistance: 1,
            p1StartPos: new Position(0, 0));
        
        var destPos = new Position(0, 1);
        game.Board.AddRock(new Rock(destPos));

        // Act & Assert: attempting to end on rock throws
        var ex = Assert.Throws<DomainException>(() =>
            game.MovePiece(p1, p1Piece.Id, BuildEtherealSegment(destPos))
        );
        Assert.Contains("obstacle", ex.Message);
    }

    [Fact]
    public void Ethereal_EndOnLake_Throws()
    {
        // Arrange: Ethereal piece at (0,0), lake at destination (0,1)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithEtherealPiece(
            p1MaxDistance: 1,
            p1StartPos: new Position(0, 0));
        
        var destPos = new Position(0, 1);
        game.Board.AddLake(new Lake(destPos));

        // Act & Assert: attempting to end on lake throws
        var ex = Assert.Throws<DomainException>(() =>
            game.MovePiece(p1, p1Piece.Id, BuildEtherealSegment(destPos))
        );
        Assert.Contains("obstacle", ex.Message);
    }
}
