using System.Collections.ObjectModel;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Tests;

/// <summary>
/// Unit tests for ice patch sliding behaviour (Issue #47 - Cycle 2).
/// Tests that non-Jump pieces slide one extra tile when landing on ice patches.
/// </summary>
public class IceSlidingTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a game in MovePhase with a piece at a specified position.
    /// </summary>
    private static (Game game, Guid playerId, Piece piece) CreateGameWithPieceInMovePhase(
        Position pieceStartPos,
        MovementType movementType = MovementType.Orthogonal,
        int maxDistance = 4,
        int movesPerTurn = 1)
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var board = new Board();

        var testPiece = new Piece(Guid.NewGuid(), "TestPiece", p1,
            EntryPointType.Borders, movementType, maxDistance, movesPerTurn);
        var p1Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"Fill{i}", p1, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var p2Piece = new Piece(Guid.NewGuid(), "P2", p2,
            EntryPointType.Borders, MovementType.Orthogonal, 1, 1);
        var p2Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"Fill{i}", p2, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var game = new Game(p1, p2, board);
        game.SetLineup(p1, new Lineup(new[] { testPiece }.Concat(p1Fill)));
        game.SetLineup(p2, new Lineup(new[] { p2Piece }.Concat(p2Fill)));
        game.Start();
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        game.PlacePiece(p1, testPiece.Id, pieceStartPos);
        game.PlacePiece(p2, p2Piece.Id, new Position(7, 7));

        return (game, p1, testPiece);
    }

    /// <summary>
    /// Builds a single-segment move list.
    /// </summary>
    private static ReadOnlyCollection<IReadOnlyList<Position>> BuildSegments(params Position[] steps)
    {
        var segment = (IReadOnlyList<Position>)steps.ToList().AsReadOnly();
        return new List<IReadOnlyList<Position>> { segment }.AsReadOnly();
    }

    // ── Basic Ice Patch Sliding Tests ─────────────────────────────────────────

    [Fact]
    public void NonJumpPiece_LandsOnIcePatch_SlidesOneExtraTile()
    {
        // Arrange: piece at (0,0), ice patch at (0,2)
        var (game, p1, piece) = CreateGameWithPieceInMovePhase(new Position(0, 0));
        game.Board.PlaceIcePatch(new Position(0, 2));

        // Act: move a piece to (0,2) — should slide to (0,3)
        game.MovePiece(p1, piece.Id, BuildSegments(new Position(0, 1), new Position(0, 2)));

        // Assert: piece ends at (0,3) after sliding
        Assert.Equal(new Position(0, 3), piece.Position);
    }

    [Fact]
    public void NonJumpPiece_SlidesAndCollectsCoin()
    {
        // Arrange: piece at (0,0), ice patch at (0,2), coin at (0,3)
        var (game, p1, piece) = CreateGameWithPieceInMovePhase(new Position(0, 0));
        game.Board.PlaceIcePatch(new Position(0, 2));
        game.Board.GetTile(new Position(0, 3)).SetOccupant(new Coin(CoinType.Silver));

        var initialScore = game.Scores[p1];

        // Act: move a piece to (0,2) — should slide to (0,3) and collect coin
        game.MovePiece(p1, piece.Id, BuildSegments(new Position(0, 1), new Position(0, 2)));

        // Assert: piece at (0,3), coin collected
        Assert.Equal(new Position(0, 3), piece.Position);
        Assert.Equal(initialScore + 1, game.Scores[p1]);
    }

    [Fact]
    public void NonJumpPiece_SlidesBlockedByBoardEdge_StopsAtEdge()
    {
        // Arrange: piece at (0,5), ice patch at (0,6), at board edge
        var (game, p1, piece) = CreateGameWithPieceInMovePhase(new Position(0, 5));
        game.Board.PlaceIcePatch(new Position(0, 6));

        // Act: move a piece to (0,6) — slide would go to (0,7) which is the edge
        game.MovePiece(p1, piece.Id, BuildSegments(new Position(0, 6)));

        // Assert: a piece stops at (0,7) (board edge)
        Assert.Equal(new Position(0, 7), piece.Position);
    }

    [Fact]
    public void NonJumpPiece_SlidesBlockedByRock_StopsBeforeRock()
    {
        // Arrange: piece at (0,0), ice patch at (0,2), rock at (0,4)
        var (game, p1, piece) = CreateGameWithPieceInMovePhase(new Position(0, 0));
        game.Board.PlaceIcePatch(new Position(0, 2));
        game.Board.AddRock(new Obstacles.Rock(new Position(0, 3)));

        // Act: move a piece to (0,2) — try to slide to (0,3) but blocked by rock
        game.MovePiece(p1, piece.Id, BuildSegments(new Position(0, 1), new Position(0, 2)));

        // Assert: piece stays at (0,2) (no slide due to an obstacle)
        Assert.Equal(new Position(0, 2), piece.Position);
    }

    [Fact]
    public void NonJumpPiece_SlidesBlockedByOtherPiece_StopsBeforePiece()
    {
        // Arrange: piece at (0,0), ice patch at (0,2), another piece at (0,3)
        var (game, p1, piece) = CreateGameWithPieceInMovePhase(new Position(0, 0));
        game.Board.PlaceIcePatch(new Position(0, 2));

        // Place another piece at (0,3)
        var p2 = game.LineupPlayerTwo!.Pieces[0];
        p2.PlaceAt(new Position(0, 3));
        game.Board.GetTile(new Position(0, 3)).SetOccupant(p2);

        // Act: move a piece to (0,2) — try to slide to (0,3) but blocked by piece
        game.MovePiece(p1, piece.Id, BuildSegments(new Position(0, 1), new Position(0, 2)));

        // Assert: piece stays at (0,2) (no slide due to piece)
        Assert.Equal(new Position(0, 2), piece.Position);
    }

    [Fact]
    public void NonJumpPiece_MultipleIcePatches_SlidesOnceOnly()
    {
        // Arrange: piece at (0,0), ice patches at (0,2) and (0,3)
        var (game, p1, piece) = CreateGameWithPieceInMovePhase(new Position(0, 0));
        game.Board.PlaceIcePatch(new Position(0, 2));
        game.Board.PlaceIcePatch(new Position(0, 3));

        // Act: move a piece to (0,2) — should slide to (0,3), then stop (no cascade)
        game.MovePiece(p1, piece.Id, BuildSegments(new Position(0, 1), new Position(0, 2)));

        // Assert: piece ends at (0,3) after one slide (does not cascade)
        Assert.Equal(new Position(0, 3), piece.Position);
    }

    [Fact]
    public void NonJumpPiece_NoIcePatch_NoSlide()
    {
        // Arrange: piece at (0,0), no ice patches
        var (game, p1, piece) = CreateGameWithPieceInMovePhase(new Position(0, 0));

        // Act: move a piece to (0,2)
        game.MovePiece(p1, piece.Id, BuildSegments(new Position(0, 1), new Position(0, 2)));

        // Assert: piece stays at (0,2) (no slide)
        Assert.Equal(new Position(0, 2), piece.Position);
    }

    // ── Diagonal Ice Patch Sliding Tests ──────────────────────────────────────

    [Fact]
    public void DiagonalPiece_LandsOnIcePatch_SlidesDiagonally()
    {
        // Arrange: piece at (0,0), ice patch at (2,2), diagonal movement
        var (game, p1, piece) = CreateGameWithPieceInMovePhase(
            new Position(0, 0),
            movementType: MovementType.Diagonal,
            maxDistance: 4);
        game.Board.PlaceIcePatch(new Position(2, 2));

        // Act: move diagonally to (2,2) — should slide to (3,3)
        game.MovePiece(p1, piece.Id, BuildSegments(new Position(1, 1), new Position(2, 2)));

        // Assert: a piece ends at (3,3)
        Assert.Equal(new Position(3, 3), piece.Position);
    }

    [Fact]
    public void DiagonalPiece_SlidesBlockedByRock_StopsBeforRock()
    {
        // Arrange: piece at (0,0), ice patch at (2,2), rock at (3,3)
        var (game, p1, piece) = CreateGameWithPieceInMovePhase(
            new Position(0, 0),
            movementType: MovementType.Diagonal);
        game.Board.PlaceIcePatch(new Position(2, 2));
        game.Board.AddRock(new Obstacles.Rock(new Position(3, 3)));

        // Act: move to (2,2) — try to slide diagonally to (3,3) but blocked by rock
        game.MovePiece(p1, piece.Id, BuildSegments(new Position(1, 1), new Position(2, 2)));

        // Assert: piece stays at (2,2) (rock blocks diagonal slide)
        Assert.Equal(new Position(2, 2), piece.Position);
    }

    // ── Jump Piece (Not Affected) Tests ───────────────────────────────────────

    [Fact]
    public void JumpPiece_OnIcePatch_NoSlide()
    {
        // Arrange: Jump piece at (0,0), ice patch at (3,3)
        var (game, p1, piece) = CreateGameWithPieceInMovePhase(
            new Position(0, 0),
            movementType: MovementType.Jump,
            maxDistance: 5);
        game.Board.PlaceIcePatch(new Position(3, 3));

        // Act: Jump to (3,3) which has an ice patch
        game.MovePiece(p1, piece.Id, BuildSegments(new Position(3, 3)));

        // Assert: a piece ends at (3,3), no slide
        Assert.Equal(new Position(3, 3), piece.Position);
    }

    // ── Multi-Move Sequence Tests ─────────────────────────────────────────────

    [Fact]
    public void MultiMoveSequence_SlideOnFirstMove_SecondMoveFromSlidePosition()
    {
        // Arrange: piece with 2 moves per turn at (0,0), ice patch at (0,2)
        var (game, p1, piece) = CreateGameWithPieceInMovePhase(
            new Position(0, 0),
            movementType: MovementType.Orthogonal,
            movesPerTurn: 2);
        game.Board.PlaceIcePatch(new Position(0, 2));

        // Act: move 1 → (0,2) slides to (0,3)
        //      move 2 → (0,5) from (0,3)
        var segment1 = (IReadOnlyList<Position>)new List<Position> { new(0, 1), new(0, 2) }.AsReadOnly();
        var segment2 = (IReadOnlyList<Position>)new List<Position> { new(0, 4), new(0, 5) }.AsReadOnly();
        var segments = new List<IReadOnlyList<Position>> { segment1, segment2 }.AsReadOnly();

        game.MovePiece(p1, piece.Id, segments);

        // Assert: piece ends at (0,5) after both moves
        Assert.Equal(new Position(0, 5), piece.Position);
    }

    // ── Ethereal Movement Tests ───────────────────────────────────────────────

    [Fact]
    public void EtherealPiece_LandsOnIcePatch_SlidesAndContinues()
    {
        // Arrange: Ethereal piece at (0,0), ice patch at (0,2), max distance 4
        var (game, p1, piece) = CreateGameWithPieceInMovePhase(
            new Position(0, 0),
            movementType: MovementType.Ethereal,
            maxDistance: 4);
        game.Board.PlaceIcePatch(new Position(0, 2));

        // Act: Ethereal move to (0,2) → slide to (0,3)
        game.MovePiece(p1, piece.Id, BuildSegments(new Position(0, 1), new Position(0, 2)));

        // Assert: a piece ends at (0,3)
        Assert.Equal(new Position(0, 3), piece.Position);
    }

    [Fact]
    public void EtherealPiece_SlidesAndContinuesMovement()
    {
        // Arrange: Ethereal piece at (0,0), ice patch at (0,2), max distance 5
        var (game, p1, piece) = CreateGameWithPieceInMovePhase(
            new Position(0, 0),
            movementType: MovementType.Ethereal,
            maxDistance: 5);
        game.Board.PlaceIcePatch(new Position(0, 2));

        // Act: move to (0,2) which slides to (0,3), then continue to (0,5)
        game.MovePiece(p1, piece.Id, BuildSegments(
            new Position(0, 1), new Position(0, 2), new Position(0, 4), new Position(0, 5)));

        // Assert: piece ends at (0,5) after sliding and continuing
        Assert.Equal(new Position(0, 5), piece.Position);
    }

    // ── Charge Movement Tests ─────────────────────────────────────────────────

    [Fact]
    public void ChargePiece_PassesThroughIcePatch_ContinuesCharging()
    {
        // Arrange: Charge piece at (0,0), ice patch at (0,3) (should NOT block charge)
        var (game, p1, piece) = CreateGameWithPieceInMovePhase(
            new Position(0, 0),
            movementType: MovementType.Charge);
        game.Board.PlaceIcePatch(new Position(0, 3));

        // Act: Charge right (first step to (0,1)) — charge passes through an ice patch and continues to board edge
        game.MovePiece(p1, piece.Id, BuildSegments(new Position(0, 1)));

        // Assert: a piece charged all the way to the board edge (0,7), passing through an ice patch
        Assert.Equal(new Position(0, 7), piece.Position);
    }

    [Fact]
    public void ChargePiece_EndsOnIcePatch_SlidesAfterCharge()
    {
        // Arrange: Charge piece at (0,0), rock at (0,5), ice patch at (0,4)
        // This causes the Charge to end at (0,4) which has an ice patch
        var (game, p1, piece) = CreateGameWithPieceInMovePhase(
            new Position(0, 0),
            movementType: MovementType.Charge);
        game.Board.AddRock(new Obstacles.Rock(new Position(0, 5)));
        game.Board.PlaceIcePatch(new Position(0, 4));

        // Act: Charge right (first step to (0,1)) → charge stops at (0,4) due to rock
        game.MovePiece(p1, piece.Id, BuildSegments(new Position(0, 1)));

        // Assert: piece charged to (0,4) where an ice patch is, then tries to slide but... 
        // wait, if charge ends at (0,4) with rock at (0,5), then slide would try (0,5) which is blocked
        // So a piece stays at (0,4)
        Assert.Equal(new Position(0, 4), piece.Position);
    }

    [Fact]
    public void ChargePiece_SlideBlockedByRock_ChargeEndsBeforeRock()
    {
        // Arrange: Charge piece at (0,0), ice patch at (0,3), rock at (0,4)
        var (game, p1, piece) = CreateGameWithPieceInMovePhase(
            new Position(0, 0),
            movementType: MovementType.Charge);
        game.Board.PlaceIcePatch(new Position(0, 3));
        game.Board.AddRock(new Obstacles.Rock(new Position(0, 4)));

        // Act: Charge right (first step to (0,1))
        game.MovePiece(p1, piece.Id, BuildSegments(new Position(0, 1)));

        // Assert: Charge reaches (0,3), tries to slide but rock blocks at (0,4), so stays at (0,3)
        Assert.Equal(new Position(0, 3), piece.Position);
    }

    [Fact]
    public void ChargePiece_SlideTowardsBoardEdge_SlidesAndStopsAtEdge()
    {
        // Arrange: Charge piece at (0,5), ice patch at (0,6)
        var (game, p1, piece) = CreateGameWithPieceInMovePhase(
            new Position(0, 5),
            movementType: MovementType.Charge);
        game.Board.PlaceIcePatch(new Position(0, 6));

        // Act: Charge right to (0,6), then slide toward the edge
        game.MovePiece(p1, piece.Id, BuildSegments(new Position(0, 6)));

        // Assert: piece ends at (0,7) (board edge)
        Assert.Equal(new Position(0, 7), piece.Position);
    }

    // ── Path Recording Tests ──────────────────────────────────────────────────

    [Fact]
    public void IceSlide_IncludedInFullPath()
    {
        // Arrange: piece at (0,0), ice patch at (0,2)
        var (game, p1, piece) = CreateGameWithPieceInMovePhase(new Position(0, 0));
        game.Board.PlaceIcePatch(new Position(0, 2));

        // Act: move to (0,2), slide to (0,3)
        game.MovePiece(p1, piece.Id, BuildSegments(new Position(0, 1), new Position(0, 2)));

        // Assert: a full path includes the slide
        var evt = game.DomainEvents.OfType<Events.PieceMoved>().Single();
        Assert.Contains(new Position(0, 1), evt.Path);
        Assert.Contains(new Position(0, 2), evt.Path);
        Assert.Contains(new Position(0, 3), evt.Path);
    }

    [Fact]
    public void IceSlideBlockedByObstacle_PathDoesNotIncludeSlidePosition()
    {
        // Arrange: piece at (0,0), ice patch at (0,2), rock at (0,3)
        var (game, p1, piece) = CreateGameWithPieceInMovePhase(new Position(0, 0));
        game.Board.PlaceIcePatch(new Position(0, 2));
        game.Board.AddRock(new Obstacles.Rock(new Position(0, 3)));

        // Act: move to (0,2), slide blocked by rock
        game.MovePiece(p1, piece.Id, BuildSegments(new Position(0, 1), new Position(0, 2)));

        // Assert: a full path does not include a blocked slide position
        var evt = game.DomainEvents.OfType<Events.PieceMoved>().Single();
        Assert.Contains(new Position(0, 1), evt.Path);
        Assert.Contains(new Position(0, 2), evt.Path);
        Assert.DoesNotContain(new Position(0, 3), evt.Path);
    }

    // ── Edge Case Tests ───────────────────────────────────────────────────────

    [Fact]
    public void Piece_SlidesNorthOnIcePatch()
    {
        // Arrange: piece at (3,0), ice patch at (1,0), moving north
        var (game, p1, piece) = CreateGameWithPieceInMovePhase(new Position(3, 0));
        game.Board.PlaceIcePatch(new Position(1, 0));

        // Act: move north to (1,0), should slide to (0,0)
        game.MovePiece(p1, piece.Id, BuildSegments(new Position(2, 0), new Position(1, 0)));

        // Assert: piece at (0,0)
        Assert.Equal(new Position(0, 0), piece.Position);
    }

    [Fact]
    public void Piece_SlidesSouthOnIcePatch()
    {
        // Arrange: piece at (3,0), ice patch at (5,0), moving south
        var (game, p1, piece) = CreateGameWithPieceInMovePhase(new Position(3, 0));
        game.Board.PlaceIcePatch(new Position(5, 0));

        // Act: move south to (5,0), should slide to (6,0)
        game.MovePiece(p1, piece.Id, BuildSegments(new Position(4, 0), new Position(5, 0)));

        // Assert: piece at (6,0)
        Assert.Equal(new Position(6, 0), piece.Position);
    }

    [Fact]
    public void Piece_SlidesWestOnIcePatch()
    {
        // Arrange: piece at (0,3), ice patch at (0,1), moving west
        var (game, p1, piece) = CreateGameWithPieceInMovePhase(new Position(0, 3));
        game.Board.PlaceIcePatch(new Position(0, 1));

        // Act: move west to (0,1), should slide to (0,0)
        game.MovePiece(p1, piece.Id, BuildSegments(new Position(0, 2), new Position(0, 1)));

        // Assert: piece at (0,0)
        Assert.Equal(new Position(0, 0), piece.Position);
    }

    [Fact]
    public void Piece_SlidesEastOnIcePatch()
    {
        // Arrange: piece at (0,3), ice patch at (0,5), moving east
        var (game, p1, piece) = CreateGameWithPieceInMovePhase(new Position(0, 3));
        game.Board.PlaceIcePatch(new Position(0, 5));

        // Act: move east to (0,5), should slide to (0,6)
        game.MovePiece(p1, piece.Id, BuildSegments(new Position(0, 4), new Position(0, 5)));

        // Assert: piece at (0,6)
        Assert.Equal(new Position(0, 6), piece.Position);
    }
}
