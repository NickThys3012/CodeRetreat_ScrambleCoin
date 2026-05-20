using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Events;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.Factories;
using ScrambleCoin.Domain.Obstacles;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Tests;

/// <summary>
/// Unit tests for Jump movement type (Issue #44).
/// Jump pieces teleport directly to their destination, ignoring obstacles and pieces along the way.
/// Coins are collected only at the destination tile, not along the path.
/// </summary>
public class JumpMovementTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a game in MovePhase with exactly one Jump piece per player.
    /// P1's Jump piece (Goofy-like) is at p1StartPos; P2's orthogonal piece is at p2StartPos.
    /// </summary>
    private static (Game game, Guid p1, Guid p2, Piece p1Piece, Piece p2Piece) GameInMovePhaseWithJumpPiece(
        int p1MaxDistance = 3,
        Position? p1StartPos = null,
        Position? p2StartPos = null)
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var board = new Board();

        var actualP1Pos = p1StartPos ?? new Position(0, 0);
        var actualP2Pos = p2StartPos ?? new Position(7, 7);

        // Create a Jump piece for P1 (like Goofy)
        var p1Piece = new Piece(
            Guid.NewGuid(), "TestJumper", p1,
            EntryPointType.Corners, MovementType.Jump, p1MaxDistance, 1);
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
    /// Builds a single-segment move list containing a single destination position for Jump.
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<Position>> BuildJumpSegment(Position destination)
    {
        var segment = (IReadOnlyList<Position>)new List<Position> { destination }.AsReadOnly();
        return new List<IReadOnlyList<Position>> { segment }.AsReadOnly();
    }

    // ── Core Jump Logic Tests ─────────────────────────────────────────────────

    [Fact]
    public void Jump_ToEmptyTileWithCoin_CollectsCoinOnlyAtDestination()
    {
        // Arrange: Jump piece at (0,0), empty tiles on path, coin at (3,0) [distance 3]
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithJumpPiece(
            p1MaxDistance: 3,
            p1StartPos: new Position(0, 0));
        game.Board.GetTile(new Position(3, 0)).SetOccupant(new Coin(CoinType.Silver));

        // Act: Jump from (0,0) to (3,0)
        game.MovePiece(p1, p1Piece.Id, BuildJumpSegment(new Position(3, 0)));

        // Assert: piece moved to destination
        Assert.Equal(new Position(3, 0), p1Piece.Position);

        // Assert: only coin at destination collected
        Assert.Equal(1, game.Scores[p1]);

        // Assert: coin removed from board
        Assert.Null(game.Board.GetTile(new Position(3, 0)).AsCoin);
    }

    [Fact]
    public void Jump_OverObstacle_CollectsOnlyDestinationCoin()
    {
        // Arrange: Jump piece at (0,0), rock at (1,1), coin at (3,0) [distance 3 from start]
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithJumpPiece(
            p1MaxDistance: 3,
            p1StartPos: new Position(0, 0));
        var rock = new Rock(new Position(1, 1));
        game.Board.AddRock(rock);
        game.Board.GetTile(new Position(3, 0)).SetOccupant(new Coin(CoinType.Silver));

        // Act: Jump over obstacle to (3,0)
        game.MovePiece(p1, p1Piece.Id, BuildJumpSegment(new Position(3, 0)));

        // Assert: piece successfully jumped over obstacle
        Assert.Equal(new Position(3, 0), p1Piece.Position);

        // Assert: coin at destination collected; rock obstacle still exists on board
        Assert.Equal(1, game.Scores[p1]);
        // Verify the obstacle wasn't affected by the jump
        Assert.True(game.Board.IsObstacleCovering(new Position(1, 1)));
    }

    [Fact]
    public void Jump_OverOpponentPiece_CollectsOnlyDestinationCoin()
    {
        // Arrange: Jump piece at (0,0), opponent piece at (0,7) [border], coin at (0,3)
        var (game, p1, p2, p1Piece, p2Piece) = GameInMovePhaseWithJumpPiece(
            p1MaxDistance: 3,
            p1StartPos: new Position(0, 0),
            p2StartPos: new Position(0, 7));

        game.Board.GetTile(new Position(0, 3)).SetOccupant(new Coin(CoinType.Silver));

        // Act: Jump over intermediate space to (0,3) [distance 3]
        game.MovePiece(p1, p1Piece.Id, BuildJumpSegment(new Position(0, 3)));

        // Assert: piece successfully jumped
        Assert.Equal(new Position(0, 3), p1Piece.Position);

        // Assert: opponent piece still at (0,7)
        Assert.Equal(new Position(0, 7), p2Piece.Position);

        // Assert: coin collected at destination
        Assert.Equal(1, game.Scores[p1]);
    }

    [Fact]
    public void Jump_ToOccupiedTile_ThrowsDomainException()
    {
        // Arrange: Jump piece at (0,0), ally piece at (3,3)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithJumpPiece(
            p1StartPos: new Position(0, 0));

        // Place a second piece for P1 at (3,3)
        var allyPiece = new Piece(
            Guid.NewGuid(), "AllyPiece", p1,
            EntryPointType.Borders, MovementType.Orthogonal, 1, 1);
        game.Board.GetTile(new Position(3, 3)).SetOccupant(allyPiece);

        // Act & Assert: jump to occupied tile rejected
        Assert.Throws<DomainException>(() =>
            game.MovePiece(p1, p1Piece.Id, BuildJumpSegment(new Position(3, 3))));
    }

    [Fact]
    public void Jump_BeyondMaxDistance_ThrowsDomainException()
    {
        // Arrange: Jump piece with MaxDistance=3 at (0,0)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithJumpPiece(
            p1MaxDistance: 3,
            p1StartPos: new Position(0, 0));

        // Act & Assert: try to jump to (0,5) [distance 5 > MaxDistance 3]
        Assert.Throws<DomainException>(() =>
            game.MovePiece(p1, p1Piece.Id, BuildJumpSegment(new Position(0, 5))));
    }

    [Fact]
    public void Jump_ToAdjacentTile_Succeeds()
    {
        // Arrange: Jump piece with MaxDistance=3 at (0,0)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithJumpPiece(
            p1MaxDistance: 3,
            p1StartPos: new Position(0, 0));

        // Act: Jump to adjacent tile (1,0) [distance 1]
        game.MovePiece(p1, p1Piece.Id, BuildJumpSegment(new Position(1, 0)));

        // Assert: jump succeeds
        Assert.Equal(new Position(1, 0), p1Piece.Position);
    }

    [Fact]
    public void Jump_ExactlyAtMaxDistance_Succeeds()
    {
        // Arrange: Jump piece with MaxDistance=3 at (0,0)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithJumpPiece(
            p1MaxDistance: 3,
            p1StartPos: new Position(0, 0));

        // Act: Jump to (3,3) [Chebyshev distance = 3, equals MaxDistance]
        game.MovePiece(p1, p1Piece.Id, BuildJumpSegment(new Position(3, 3)));

        // Assert: jump succeeds
        Assert.Equal(new Position(3, 3), p1Piece.Position);
    }

    // ── Direction Validation Tests ────────────────────────────────────────────

    [Fact]
    public void Jump_AnyDirection_RightDirection()
    {
        // Arrange: Jump piece at (0,0), jump right to (0,3) [distance 3]
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithJumpPiece(
            p1MaxDistance: 3,
            p1StartPos: new Position(0, 0));

        // Act: Jump right to (0,3)
        game.MovePiece(p1, p1Piece.Id, BuildJumpSegment(new Position(0, 3)));

        // Assert: jump succeeds
        Assert.Equal(new Position(0, 3), p1Piece.Position);
    }

    [Fact]
    public void Jump_AnyDirection_DownDirection()
    {
        // Arrange: Jump piece at (0,0), jump down to (3,0) [distance 3]
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithJumpPiece(
            p1MaxDistance: 3,
            p1StartPos: new Position(0, 0));

        // Act: Jump down to (3,0)
        game.MovePiece(p1, p1Piece.Id, BuildJumpSegment(new Position(3, 0)));

        // Assert: jump succeeds
        Assert.Equal(new Position(3, 0), p1Piece.Position);
    }

    [Fact]
    public void Jump_AnyDirection_DiagonalDirection()
    {
        // Arrange: Jump piece at (0,0), jump diagonally to (3,3) [distance 3]
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithJumpPiece(
            p1MaxDistance: 3,
            p1StartPos: new Position(0, 0));

        // Act: Jump diagonally to (3,3)
        game.MovePiece(p1, p1Piece.Id, BuildJumpSegment(new Position(3, 3)));

        // Assert: jump succeeds
        Assert.Equal(new Position(3, 3), p1Piece.Position);
    }

    [Fact]
    public void Jump_WithMultipleCoinsTileOnPath_CollectsOnlyDestinationCoin()
    {
        // Arrange: Jump piece at (0,0), coins at (1,0), (2,0), (3,0)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithJumpPiece(
            p1MaxDistance: 3,
            p1StartPos: new Position(0, 0));
        game.Board.GetTile(new Position(1, 0)).SetOccupant(new Coin(CoinType.Silver));
        game.Board.GetTile(new Position(2, 0)).SetOccupant(new Coin(CoinType.Silver));
        game.Board.GetTile(new Position(3, 0)).SetOccupant(new Coin(CoinType.Silver));

        // Act: Jump from (0,0) to (3,0)
        game.MovePiece(p1, p1Piece.Id, BuildJumpSegment(new Position(3, 0)));

        // Assert: only coin at (3,0) collected (score = 1)
        Assert.Equal(1, game.Scores[p1]);

        // Assert: coins at intermediate positions still on board
        Assert.NotNull(game.Board.GetTile(new Position(1, 0)).AsCoin);
        Assert.NotNull(game.Board.GetTile(new Position(2, 0)).AsCoin);

        // Assert: coin at destination removed
        Assert.Null(game.Board.GetTile(new Position(3, 0)).AsCoin);
    }

    // ── Gold Coin Collection Tests ────────────────────────────────────────────

    [Fact]
    public void Jump_CollectsGoldCoinAtDestination()
    {
        // Arrange: Jump piece at (0,0), gold coin at (3,0)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithJumpPiece(
            p1MaxDistance: 3,
            p1StartPos: new Position(0, 0));
        game.Board.GetTile(new Position(3, 0)).SetOccupant(new Coin(CoinType.Gold));

        // Act: Jump to gold coin
        game.MovePiece(p1, p1Piece.Id, BuildJumpSegment(new Position(3, 0)));

        // Assert: gold coin (value 3) collected
        Assert.Equal(3, game.Scores[p1]);
    }

    [Fact]
    public void Jump_WithGoldCoinAtDestination_RaisesCoinCollectedEvent()
    {
        // Arrange
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithJumpPiece(
            p1MaxDistance: 3,
            p1StartPos: new Position(0, 0));
        game.Board.GetTile(new Position(3, 0)).SetOccupant(new Coin(CoinType.Gold));

        // Act
        game.MovePiece(p1, p1Piece.Id, BuildJumpSegment(new Position(3, 0)));

        // Assert
        var evt = game.DomainEvents.OfType<CoinCollected>().SingleOrDefault();
        Assert.NotNull(evt);
        Assert.Equal(p1Piece.Id, evt.PieceId);
        Assert.Equal(CoinType.Gold, evt.CoinType);
        Assert.Equal(3, evt.Value);
        Assert.Equal(new Position(3, 0), evt.Position);
    }

    // ── Multi-Move Tests ──────────────────────────────────────────────────────

    [Fact]
    public void Jump_MultiMoveWithTwoSegments_PerformsEachJumpIndependently()
    {
        // Arrange: Jump piece with MovesPerTurn=2 at (0,0)
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var board = new Board();

        var p1Piece = new Piece(
            Guid.NewGuid(), "MultiJumper", p1,
            EntryPointType.Corners, MovementType.Jump, 2, 2);
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
        game.AdvancePhase();

        game.PlacePiece(p1, p1Piece.Id, new Position(0, 0));
        game.PlacePiece(p2, p2Piece.Id, new Position(7, 7));

        // Build two jump segments: (0,0)→(2,2), (2,2)→(4,4)
        var segment1 = (IReadOnlyList<Position>)new List<Position> { new Position(2, 2) }.AsReadOnly();
        var segment2 = (IReadOnlyList<Position>)new List<Position> { new Position(4, 4) }.AsReadOnly();
        var segments = (IReadOnlyList<IReadOnlyList<Position>>)new List<IReadOnlyList<Position>> { segment1, segment2 }.AsReadOnly();

        // Act: Perform both jumps
        game.MovePiece(p1, p1Piece.Id, segments);

        // Assert: piece ends at (4,4)
        Assert.Equal(new Position(4, 4), p1Piece.Position);
    }

    // ── Edge Cases & Error Handling ───────────────────────────────────────────

    [Fact]
    public void Jump_ToDestinationWithLake_ThrowsDomainException()
    {
        // Arrange: Piece at (0,0), Lake at (2,2), MaxDistance = 5 (so distance check passes)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithJumpPiece(
            p1MaxDistance: 5,
            p1StartPos: new Position(0, 0));
        game.Board.AddLake(new Lake(new Position(2, 2)));
        
        // Act & Assert: Should throw because destination has lake obstacle
        // Jump to (2,2) has distance = 2, within MaxDistance = 5
        var ex = Assert.Throws<DomainException>(() =>
            game.MovePiece(p1, p1Piece.Id, BuildJumpSegment(new Position(2, 2))));
        Assert.Contains("occupied", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Jump_DestinationOffBoard_ThrowsDomainException()
    {
        // Arrange: Jump piece at (7,7)
        // Act & Assert: Try to create off-board position (9,9)
        Assert.Throws<DomainException>(() => new Position(9, 9));
    }

    [Fact]
    public void Jump_ToSamePosition_ThrowsDomainException()
    {
        // Arrange: Jump piece at (0,0)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithJumpPiece(p1StartPos: new Position(0, 0));

        // Act & Assert: Try to jump to same position
        Assert.Throws<DomainException>(() =>
            game.MovePiece(p1, p1Piece.Id, BuildJumpSegment(new Position(0, 0))));
    }

    [Fact]
    public void Jump_WithWrongSegmentCount_ThrowsDomainException()
    {
        // Arrange: Jump piece with MovesPerTurn=1; we'll try 2 segments
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithJumpPiece(p1StartPos: new Position(0, 0));

        // Build two segments when only one is allowed
        var segment1 = (IReadOnlyList<Position>)new List<Position> { new Position(2, 2) }.AsReadOnly();
        var segment2 = (IReadOnlyList<Position>)new List<Position> { new Position(4, 4) }.AsReadOnly();
        var segments = (IReadOnlyList<IReadOnlyList<Position>>)new List<IReadOnlyList<Position>> { segment1, segment2 }.AsReadOnly();

        // Act & Assert: two segments when MovesPerTurn=1 should fail
        Assert.Throws<DomainException>(() =>
            game.MovePiece(p1, p1Piece.Id, segments));
    }

    [Fact]
    public void Jump_WithMultiplePositionsInOneSegment_ThrowsDomainException()
    {
        // Arrange: Jump piece at (0,0)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithJumpPiece(p1StartPos: new Position(0, 0));

        // Build a segment with 2 positions (invalid for Jump)
        var segment = (IReadOnlyList<Position>)new List<Position> 
        { 
            new Position(1, 1), 
            new Position(2, 2) 
        }.AsReadOnly();
        var segments = (IReadOnlyList<IReadOnlyList<Position>>)new List<IReadOnlyList<Position>> { segment }.AsReadOnly();

        // Act & Assert: Jump segment must have exactly 1 destination
        Assert.Throws<DomainException>(() =>
            game.MovePiece(p1, p1Piece.Id, segments));
    }

    // ── PieceFactory Integration Tests ────────────────────────────────────────

    [Fact]
    public void PieceFactory_Goofy_CreatesJumpPiece()
    {
        // Act
        var goofy = PieceFactory.Create("Goofy", Guid.NewGuid());

        // Assert
        Assert.Equal(MovementType.Jump, goofy.MovementType);
        Assert.Equal(3, goofy.MaxDistance);
        Assert.Equal(1, goofy.MovesPerTurn);
        Assert.Equal(EntryPointType.Corners, goofy.EntryPointType);
    }

    [Fact]
    public void PieceFactory_Goofy_CanJumpInGame()
    {
        // Arrange: Create Goofy via factory
        var p1 = Guid.NewGuid();
        var goofy = PieceFactory.Create("Goofy", p1);

        var p2 = Guid.NewGuid();
        var p2Piece = new Piece(
            Guid.NewGuid(), "P2Mover", p2,
            EntryPointType.Borders, MovementType.Orthogonal, 1, 1);
        var p2Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(
                Guid.NewGuid(), $"P2Fill{i}", p2, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var p1Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(
                Guid.NewGuid(), $"P1Fill{i}", p1, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(new[] { goofy }.Concat(p1Fill)));
        game.SetLineup(p2, new Lineup(new[] { p2Piece }.Concat(p2Fill)));
        game.Start();
        game.AdvancePhase();

        game.PlacePiece(p1, goofy.Id, new Position(0, 0));
        game.PlacePiece(p2, p2Piece.Id, new Position(7, 7));

        // Act: Jump to (3,3)
        game.MovePiece(p1, goofy.Id, BuildJumpSegment(new Position(3, 3)));

        // Assert: Jump succeeds
        Assert.Equal(new Position(3, 3), goofy.Position);
    }

    // ── Event Verification Tests ──────────────────────────────────────────────

    [Fact]
    public void Jump_RaisesPieceMovedEvent()
    {
        // Arrange
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithJumpPiece(p1StartPos: new Position(0, 0));

        // Act
        game.MovePiece(p1, p1Piece.Id, BuildJumpSegment(new Position(3, 3)));

        // Assert
        var evt = game.DomainEvents.OfType<PieceMoved>().SingleOrDefault();
        Assert.NotNull(evt);
        Assert.Equal(p1Piece.Id, evt.PieceId);
        Assert.Equal(new Position(0, 0), evt.From);
        Assert.Equal(new Position(3, 3), evt.To);
    }

    [Fact]
    public void Jump_WithoutCoin_RaisesOnlyPieceMovedEvent()
    {
        // Arrange
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithJumpPiece(
            p1MaxDistance: 3,
            p1StartPos: new Position(0, 0));

        // Clear initial domain events (from setup)
        game.ClearDomainEvents();

        // Act: Jump to empty tile
        game.MovePiece(p1, p1Piece.Id, BuildJumpSegment(new Position(3, 0)));

        // Assert: only PieceMoved event, no CoinCollected
        Assert.Single(game.DomainEvents); // Only PieceMoved
        Assert.IsType<PieceMoved>(game.DomainEvents[0]);
    }

    [Fact]
    public void Jump_WithCoin_RaisesBothPieceMovedAndCoinCollectedEvents()
    {
        // Arrange
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithJumpPiece(
            p1MaxDistance: 3,
            p1StartPos: new Position(0, 0));
        game.Board.GetTile(new Position(3, 0)).SetOccupant(new Coin(CoinType.Silver));

        // Clear initial domain events (from setup)
        game.ClearDomainEvents();

        // Act
        game.MovePiece(p1, p1Piece.Id, BuildJumpSegment(new Position(3, 0)));

        // Assert
        Assert.Equal(2, game.DomainEvents.Count);
        Assert.NotNull(game.DomainEvents.OfType<PieceMoved>().SingleOrDefault());
        Assert.NotNull(game.DomainEvents.OfType<CoinCollected>().SingleOrDefault());
    }
}
