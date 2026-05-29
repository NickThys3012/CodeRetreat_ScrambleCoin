using System.Collections.ObjectModel;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.Factories;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Tests;

/// <summary>
/// Unit tests for multistep movement sequences (Issue #48).
/// Tests per-segment movement type validation for pieces like Cogsworth, Lumiere, Remy, Anna, Olaf, Kristoff.
/// </summary>
public class MultiStepMovementTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a game in MovePhase with exactly one multistep piece for player 1.
    /// </summary>
    private static (Game game, Guid p1, Guid p2, Piece piece, Position startPos) GameInMovePhaseWithMultiStepPiece(string pieceName)
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var board = new Board();

        var p1Piece = PieceFactory.Create(pieceName, p1);
        var p1Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"P1Fill{i}", p1, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var p2Piece = new Piece(Guid.NewGuid(), "P2Mover", p2,
            EntryPointType.Borders, MovementType.Orthogonal, 1, 1);
        var p2Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"P2Fill{i}", p2, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var game = new Game(p1, p2, board);
        game.SetLineup(p1, new Lineup(new[] { p1Piece }.Concat(p1Fill)));
        game.SetLineup(p2, new Lineup(new[] { p2Piece }.Concat(p2Fill)));
        game.Start();
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        // Choose the appropriate starting position based on an entry point type
        var p1StartPos = p1Piece.EntryPointType == EntryPointType.Corners ? new Position(0, 0) : new Position(0, 3);
        var p2StartPos = new Position(7, 3);

        // Place both pieces to auto-advance to MovePhase
        game.PlacePiece(p1, p1Piece.Id, p1StartPos);
        game.PlacePiece(p2, p2Piece.Id, p2StartPos);

        return (game, p1, p2, p1Piece, p1StartPos);
    }

    /// <summary>
    /// Builds a multi-segment move list.
    /// </summary>
    private static ReadOnlyCollection<IReadOnlyList<Position>> BuildSegments(params Position[][] segments)
    {
        return segments.Select(IReadOnlyList<Position> (s) => s.ToList().AsReadOnly()).ToList().AsReadOnly();
    }

    // ── Test 1: Cogsworth valid sequence (Any → Orthogonal) ──────────────────

    [Fact]
    public void CogsworthValidSequence_Any1ThenOrthogonal2_Succeeds()
    {
        // Arrange: Cogsworth at (0, 3) [Borders entry]
        var (game, p1, _, piece, startPos) = GameInMovePhaseWithMultiStepPiece("Cogsworth");

        // Act: Segment 1 (Any direction): 1 tile right → (0, 4)
        //      Segment 2 (Orthogonal): 2 tiles down → (2, 4)
        var segments = BuildSegments(
            [new Position(0, 4)],
            [new Position(1, 4), new Position(2, 4)]
        );

        game.MovePiece(p1, piece.Id, segments);
        Assert.Equal(new Position(2, 4), piece.Position);
    }

    // ── Test 2: Cogsworth wrong type in segment 2 ────────────────────────────

    [Fact]
    public void CogsworthWrongTypeSegment2_Diagonal2InsteadOfOrthogonal_Throws()
    {
        // Arrange: Cogsworth at (0, 3) [Borders entry]
        var (game, p1, _, piece, startPos) = GameInMovePhaseWithMultiStepPiece("Cogsworth");

        // Act: Segment 1 (Any): 1 tile right → (0, 4)
        //      Segment 2 (should be Orthogonal, but we try Diagonal)
        var segments = BuildSegments(
            [new Position(0, 4)],
            [new Position(1, 5)] // Diagonal (wrong - should be Orthogonal)
        );

        // Act & Assert: Should throw
        var ex = Assert.Throws<DomainException>(() => game.MovePiece(p1, piece.Id, segments));
        Assert.Contains("not orthogonal", ex.Message);
    }

    // ── Test 3: Cogsworth exceeds max distance in segment 2 ──────────────────

    [Fact]
    public void CogsworthExceedsMaxDistanceSegment2_Orthogonal3InsteadOf2_Throws()
    {
        // Arrange: Cogsworth at (0, 3) [Borders entry]
        var (game, p1, _, piece, startPos) = GameInMovePhaseWithMultiStepPiece("Cogsworth");

        // Act: Segment 1: 1 tile right → (0, 4)
        //      Segment 2: Attempt 3 orthogonal tiles down (max is 2)
        var segments = BuildSegments(
            [new Position(0, 4)],
            [new Position(1, 4), new Position(2, 4), new Position(3, 4)]
        );

        // Act & Assert: Should throw
        var ex = Assert.Throws<DomainException>(() => game.MovePiece(p1, piece.Id, segments));
        Assert.Contains("step(s)", ex.Message);
    }

    // ── Test 4: Lumiere valid sequence (Any → Diagonal) ─────────────────────

    [Fact]
    public void LumiereValidSequence_Any1ThenDiagonal2_Succeeds()
    {
        // Arrange: Lumiere at (0, 3) [Borders entry]
        var (game, p1, _, piece, startPos) = GameInMovePhaseWithMultiStepPiece("Lumiere");

        // Act: Segment 1 (Any): 1 tile right → (0, 4)
        //      Segment 2 (Diagonal): 2 diagonal → (2, 6)
        var segments = BuildSegments(
            [new Position(0, 4)],
            [new Position(1, 5), new Position(2, 6)]
        );

        game.MovePiece(p1, piece.Id, segments);
        Assert.Equal(new Position(2, 6), piece.Position);
    }

    // ── Test 5: Lumiere wrong type in segment 2 ──────────────────────────────

    [Fact]
    public void LumiereWrongTypeSegment2_Orthogonal2InsteadOfDiagonal_Throws()
    {
        // Arrange: Lumiere at (0, 3) [Borders entry]
        var (game, p1, _, piece, startPos) = GameInMovePhaseWithMultiStepPiece("Lumiere");

        // Act: Segment 1 (Any): 1 tile right → (0, 4)
        //      Segment 2 (should be Diagonal, but we try Orthogonal)
        var segments = BuildSegments(
            [new Position(0, 4)],
            [new Position(1, 4), new Position(2, 4)] // Orthogonal (wrong)
        );

        // Act & Assert: Should throw
        var ex = Assert.Throws<DomainException>(() => game.MovePiece(p1, piece.Id, segments));
        Assert.Contains("not diagonal", ex.Message);
    }

    // ── Test 6: Remy - 2 independent diagonal moves ───────────────────────────

    [Fact]
    public void RemyValidSequence_Diagonal2ThenDiagonal2_Succeeds()
    {
        // Arrange: Remy at (0, 3) [Borders]
        var (game, p1, _, piece, startPos) = GameInMovePhaseWithMultiStepPiece("Remy");

        // Act: Segment 1 (Diagonal): 2 tiles diagonal → (2, 5)
        //      Segment 2 (Diagonal): 2 tiles diagonal → (4, 7)
        var segments = BuildSegments(
            [new Position(1, 4), new Position(2, 5)],
            [new Position(3, 6), new Position(4, 7)]
        );

        game.MovePiece(p1, piece.Id, segments);
        Assert.Equal(new Position(4, 7), piece.Position);
    }

    // ── Test 7: Anna - all 3 orthogonal moves required ───────────────────────

    [Fact]
    public void AnnaValidSequence_Orthogonal1x3_Succeeds()
    {
        // Arrange: Anna at (0, 3) [Borders]
        var (game, p1, _, piece, startPos) = GameInMovePhaseWithMultiStepPiece("Anna");

        // Act: All 3 segments of 1 orthogonal tile each
        var segments = BuildSegments(
            [new Position(0, 4)],
            [new Position(1, 4)],
            [new Position(1, 5)]
        );

        game.MovePiece(p1, piece.Id, segments);
        Assert.Equal(new Position(1, 5), piece.Position);
    }

    // ── Test 8: Anna - missing required segment ──────────────────────────────

    [Fact]
    public void AnnaIncompleteSequence_Only2Of3Segments_Throws()
    {
        // Arrange: Anna at (0, 3) — requires all 3 moves
        var (game, p1, _, piece, startPos) = GameInMovePhaseWithMultiStepPiece("Anna");

        // Act: Try to submit only 2 of 3 required segments
        var segments = BuildSegments(
            [new Position(0, 4)],
            [new Position(1, 4)]
            // Missing segment 3
        );

        // Act & Assert: Should throw
        var ex = Assert.Throws<DomainException>(() => game.MovePiece(p1, piece.Id, segments));
        Assert.Contains("requires exactly", ex.Message);
        Assert.Contains("3", ex.Message);
    }

    // ── Test 9: Olaf - 2 any-direction moves ─────────────────────────────────

    [Fact]
    public void OlafValidSequence_Any1x2_Succeeds()
    {
        // Arrange: Olaf at (0, 3) [Anywhere entry]
        var (game, p1, _, piece, startPos) = GameInMovePhaseWithMultiStepPiece("Olaf");

        // Act: 2 segments of 1 any-direction tile each
        var segments = BuildSegments(
            [new Position(0, 4)],
            [new Position(1, 4)]
        );

        game.MovePiece(p1, piece.Id, segments);
        Assert.Equal(new Position(1, 4), piece.Position);
    }

    // ── Test 10: Kristoff - 3 diagonal moves ────────────────────────────────

    [Fact]
    public void KristoffValidSequence_Diagonal1x3_Succeeds()
    {
        // Arrange: Kristoff at (0, 3) [Borders]
        var (game, p1, _, piece, startPos) = GameInMovePhaseWithMultiStepPiece("Kristoff");
        
        // Adjust to (1, 1) for better diagonal testing
        var board = game.Board;
        var currentTile = board.GetTile(startPos);
        currentTile.ClearOccupant();
        var newPos = new Position(1, 1);
        piece.PlaceAt(newPos);
        board.GetTile(newPos).SetOccupant(piece);

        // Act: 3 diagonal moves of 1 tile each
        var segments = BuildSegments(
            [new Position(2, 2)],
            [new Position(3, 3)],
            [new Position(4, 4)]
        );

        game.MovePiece(p1, piece.Id, segments);
        Assert.Equal(new Position(4, 4), piece.Position);
    }

    // ── Test 11: Kristoff - missing required segment ──────────────────────────

    [Fact]
    public void KristoffIncompleteSequence_Only2Of3Segments_Throws()
    {
        // Arrange: Kristoff — requires all 3 moves
        var (game, p1, _, piece, startPos) = GameInMovePhaseWithMultiStepPiece("Kristoff");
        var board = game.Board;
        var currentTile = board.GetTile(startPos);
        currentTile.ClearOccupant();
        var newPos = new Position(1, 1);
        piece.PlaceAt(newPos);
        board.GetTile(newPos).SetOccupant(piece);

        // Act: Try 2 of 3 segments
        var segments = BuildSegments(
            [new Position(2, 2)],
            [new Position(3, 3)]
            // Missing segment 3
        );

        // Act & Assert: Should throw
        var ex = Assert.Throws<DomainException>(() => game.MovePiece(p1, piece.Id, segments));
        Assert.Contains("requires exactly", ex.Message);
    }

    // ── Test 12: Piece factory creates pieces with the correct segment metadata ───

    [Fact]
    public void PieceFactoryCogsworth_HasCorrectSegmentMovementTypes()
    {
        // Arrange & Act
        var playerId = Guid.NewGuid();
        var piece = PieceFactory.Create("Cogsworth", playerId);

        // Assert
        Assert.Equal(2, piece.MovesPerTurn);
        Assert.NotNull(piece.SegmentMovementTypes);
        Assert.Equal(2, piece.SegmentMovementTypes.Count);
        Assert.Equal(MovementType.AnyDirection, piece.SegmentMovementTypes[0]);
        Assert.Equal(MovementType.Orthogonal, piece.SegmentMovementTypes[1]);
    }

    [Fact]
    public void PieceFactoryAnna_HasCorrectSegmentMovementTypes()
    {
        // Arrange & Act
        var playerId = Guid.NewGuid();
        var piece = PieceFactory.Create("Anna", playerId);

        // Assert
        Assert.Equal(3, piece.MovesPerTurn);
        Assert.NotNull(piece.SegmentMovementTypes);
        Assert.Equal(3, piece.SegmentMovementTypes.Count);
        Assert.All(piece.SegmentMovementTypes, mt => Assert.Equal(MovementType.Orthogonal, mt));
    }

    [Fact]
    public void PieceFactoryKristoff_HasCorrectSegmentMaxDistances()
    {
        // Arrange & Act
        var playerId = Guid.NewGuid();
        var piece = PieceFactory.Create("Kristoff", playerId);

        // Assert
        Assert.Equal(3, piece.MovesPerTurn);
        Assert.NotNull(piece.SegmentMaxDistances);
        Assert.Equal(3, piece.SegmentMaxDistances.Count);
        Assert.All(piece.SegmentMaxDistances, d => Assert.Equal(1, d));
    }

    // ── Test 13: GetSegmentMovementType fallback ─────────────────────────────

    [Fact]
    public void GetSegmentMovementType_WithoutSegmentMetadata_ReturnsPrimaryMovementType()
    {
        // Arrange: Single-move piece
        var playerId = Guid.NewGuid();
        var piece = PieceFactory.Create("Mickey", playerId);

        // Act
        var segType = piece.GetSegmentMovementType(0);

        // Assert
        Assert.Equal(MovementType.Orthogonal, segType);
    }

    [Fact]
    public void GetSegmentMaxDistance_WithoutSegmentMetadata_ReturnsPrimaryMaxDistance()
    {
        // Arrange: Single-move piece
        var playerId = Guid.NewGuid();
        var piece = PieceFactory.Create("Mickey", playerId);

        // Act
        var maxDist = piece.GetSegmentMaxDistance(0);

        // Assert
        Assert.Equal(3, maxDist);
    }

    // ── Test 14: Remy wrong type in the first segment ────────────────────────────

    [Fact]
    public void RemyWrongTypeSegment1_Orthogonal2InsteadOfDiagonal_Throws()
    {
        // Arrange: Remy at (0, 3)
        var (game, p1, _, piece, startPos) = GameInMovePhaseWithMultiStepPiece("Remy");

        // Act: Segment 1 should be diagonal, but we try orthogonal
        var segments = BuildSegments(
            [new Position(0, 4), new Position(0, 5)],  // Orthogonal (wrong)
            [new Position(1, 6), new Position(2, 7)]
        );

        // Act & Assert: Should throw
        var ex = Assert.Throws<DomainException>(() => game.MovePiece(p1, piece.Id, segments));
        Assert.Contains("not diagonal", ex.Message);
    }

    // ── Test 15: All 6 multistep pieces can be created ──────────────────────

    [Theory]
    [InlineData("Cogsworth")]
    [InlineData("Lumiere")]
    [InlineData("Remy")]
    [InlineData("Anna")]
    [InlineData("Olaf")]
    [InlineData("Kristoff")]
    public void AllMultiStepPieces_CanBeCreated_WithCorrectMovesPerTurn(string pieceName)
    {
        // Arrange & Act
        var playerId = Guid.NewGuid();
        var piece = PieceFactory.Create(pieceName, playerId);

        // Assert
        Assert.True(piece.MovesPerTurn >= 2);
        Assert.NotNull(piece.SegmentMovementTypes);
        Assert.Equal(piece.MovesPerTurn, piece.SegmentMovementTypes.Count);
    }
}
