using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Factories;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Tests;

/// <summary>
/// Unit tests for Elsa ice patches feature (Issue #47).
/// </summary>
public class IcePatchesTests
{
    // ── Board API Tests ────────────────────────────────────────────────────────

    [Fact]
    public void Board_HasIcePatch_ReturnsFalseWhenNoIcePatch()
    {
        var board = new Board();
        Assert.False(board.HasIcePatch(new Position(0, 0)));
    }

    [Fact]
    public void Board_PlaceIcePatch_MarksPositionAsHavingIcePatch()
    {
        var board = new Board();
        board.PlaceIcePatch(new Position(2, 3));
        Assert.True(board.HasIcePatch(new Position(2, 3)));
    }

    [Fact]
    public void Board_PlaceIcePatch_Idempotent_CanPlaceMultipleTimes()
    {
        var board = new Board();
        board.PlaceIcePatch(new Position(2, 3));
        board.PlaceIcePatch(new Position(2, 3)); // Place again
        Assert.True(board.HasIcePatch(new Position(2, 3)));
    }

    [Fact]
    public void Board_GetIcePatches_ReturnsAllIcePatches()
    {
        var board = new Board();
        board.PlaceIcePatch(new Position(1, 1));
        board.PlaceIcePatch(new Position(2, 2));
        board.PlaceIcePatch(new Position(3, 3));

        var patches = board.GetIcePatches();
        Assert.Equal(3, patches.Count);
        Assert.Contains(new Position(1, 1), patches);
        Assert.Contains(new Position(2, 2), patches);
        Assert.Contains(new Position(3, 3), patches);
    }

    [Fact]
    public void Board_ClearIcePatches_RemovesAllPatches()
    {
        var board = new Board();
        board.PlaceIcePatch(new Position(1, 1));
        board.PlaceIcePatch(new Position(2, 2));

        board.ClearIcePatches();

        Assert.False(board.HasIcePatch(new Position(1, 1)));
        Assert.False(board.HasIcePatch(new Position(2, 2)));
        Assert.Empty(board.GetIcePatches());
    }

    // ── Piece.IsElsa Tests ─────────────────────────────────────────────────────

    [Fact]
    public void Piece_IsElsa_ReturnsTrueForElsaPiece()
    {
        var elsaPiece = PieceFactory.Create("Elsa", Guid.NewGuid());
        Assert.True(elsaPiece.IsElsa);
    }

    [Fact]
    public void Piece_IsElsa_ReturnsFalseForNonElsaPiece()
    {
        var mickeyPiece = PieceFactory.Create("Mickey", Guid.NewGuid());
        Assert.False(mickeyPiece.IsElsa);
    }

    [Fact]
    public void Piece_IsElsa_CaseInsensitive()
    {
        var elsaPiece = new Piece(
            "elsa", // lowercase
            Guid.NewGuid(),
            EntryPointType.Borders,
            MovementType.Orthogonal,
            4,
            1);
        Assert.True(elsaPiece.IsElsa);
    }

    // ── PieceFactory Elsa Addition Tests ──────────────────────────────────────

    [Fact]
    public void PieceFactory_Create_Elsa_HasCorrectStats()
    {
        var elsaPiece = PieceFactory.Create("Elsa", Guid.NewGuid());

        Assert.Equal("Elsa", elsaPiece.Name);
        Assert.Equal(EntryPointType.Borders, elsaPiece.EntryPointType);
        Assert.Equal(MovementType.Orthogonal, elsaPiece.MovementType);
        Assert.Equal(4, elsaPiece.MaxDistance);
        Assert.Equal(1, elsaPiece.MovesPerTurn);
    }

    // ── Elsa Ice Patch Placement Tests ─────────────────────────────────────────

    private static (Game game, Guid p1, Guid p2) CreateGameInMovePhase()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var board = new Board();

        var elsaPiece = PieceFactory.Create("Elsa", p1);
        var p1Fillers = Enumerable.Range(0, 4)
            .Select(i => new Piece($"Fill{i}", p1, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var p2Piece = new Piece("P2", p2, EntryPointType.Borders, MovementType.Orthogonal, 1, 1);
        var p2Fillers = Enumerable.Range(0, 4)
            .Select(i => new Piece($"Fill{i}", p2, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var game = new Game(p1, p2, board);
        game.SetLineup(p1, new Lineup(new List<Piece> { elsaPiece }.Concat(p1Fillers).ToList()));
        game.SetLineup(p2, new Lineup(new List<Piece> { p2Piece }.Concat(p2Fillers).ToList()));
        game.Start();

        game.AdvancePhase(); // CoinSpawn → PlacePhase
        game.PlacePiece(p1, elsaPiece.Id, new Position(0, 0));
        game.PlacePiece(p2, p2Piece.Id, new Position(7, 7));

        return (game, p1, p2);
    }

    [Fact]
    public void ElsaMove_PlacesIcePatchesOnExitTiles()
    {
        // Arrange
        var (game, p1, _) = CreateGameInMovePhase();

        // Act: Move Elsa from (0,0) to (0,3) via (0,1), (0,2), (0,3)
        var segment = (IReadOnlyList<Position>)new List<Position> 
        { 
            new(0, 1),
            new(0, 2),
            new(0, 3)
        }.AsReadOnly();
        var segments = new List<IReadOnlyList<Position>> { segment }.AsReadOnly();

        var elsaPiece = game.LineupPlayerOne!.Pieces[0];
        game.MovePiece(p1, elsaPiece.Id, segments);

        // Assert: ice patches on (0,1) and (0,2), but NOT on (0,0) or (0,3)
        Assert.True(game.Board.HasIcePatch(new Position(0, 1)), "Ice patch should be at (0,1)");
        Assert.True(game.Board.HasIcePatch(new Position(0, 2)), "Ice patch should be at (0,2)");
        Assert.False(game.Board.HasIcePatch(new Position(0, 0)), "No ice patch at start (0,0)");
        Assert.False(game.Board.HasIcePatch(new Position(0, 3)), "No ice patch at destination (0,3)");
    }

    [Fact]
    public void ElsaMove_SingleStepMove_NoIcePatchesPlaced()
    {
        // Arrange
        var (game, p1, _) = CreateGameInMovePhase();

        // Act: Move Elsa from (0,0) to (0,1) - only 1 step
        var segment = (IReadOnlyList<Position>)new List<Position> { new(0, 1) }.AsReadOnly();
        var segments = new List<IReadOnlyList<Position>> { segment }.AsReadOnly();

        var elsaPiece = game.LineupPlayerOne!.Pieces[0];
        game.MovePiece(p1, elsaPiece.Id, segments);

        // Assert: no ice patches since start and end are the only positions
        Assert.False(game.Board.HasIcePatch(new Position(0, 0)));
        Assert.False(game.Board.HasIcePatch(new Position(0, 1)));
    }

    [Fact]
    public void ElsaMove_TwoStepMove_IcePatchOnMiddleTile()
    {
        // Arrange
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var board = new Board();

        var elsaPiece = PieceFactory.Create("Elsa", p1);
        var p1Fillers = Enumerable.Range(0, 4)
            .Select(i => new Piece($"Fill{i}", p1, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var p2Piece = new Piece("P2", p2, EntryPointType.Borders, MovementType.Orthogonal, 1, 1);
        var p2Fillers = Enumerable.Range(0, 4)
            .Select(i => new Piece($"Fill{i}", p2, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var game = new Game(p1, p2, board);
        game.SetLineup(p1, new Lineup(new List<Piece> { elsaPiece }.Concat(p1Fillers).ToList()));
        game.SetLineup(p2, new Lineup(new List<Piece> { p2Piece }.Concat(p2Fillers).ToList()));
        game.Start();

        game.AdvancePhase(); // CoinSpawn → PlacePhase
        // Place Elsa at (0,0) on the border, move to (0,2)
        game.PlacePiece(p1, elsaPiece.Id, new Position(0, 0));
        game.PlacePiece(p2, p2Piece.Id, new Position(7, 7));

        // Act: Move Elsa from (0,0) to (0,2) via (0,1), (0,2)
        var segment = (IReadOnlyList<Position>)new List<Position> 
        { 
            new(0, 1),
            new(0, 2)
        }.AsReadOnly();
        var segments = new List<IReadOnlyList<Position>> { segment }.AsReadOnly();

        game.MovePiece(p1, elsaPiece.Id, segments);

        // Assert: ice patch only at (0,1), not start (0,0) or end (0,2)
        Assert.True(game.Board.HasIcePatch(new Position(0, 1)));
        Assert.False(game.Board.HasIcePatch(new Position(0, 0)));
        Assert.False(game.Board.HasIcePatch(new Position(0, 2)));
    }

    [Fact]
    public void JumpPiece_UnaffectedByIcePatches()
    {
        // Arrange: Create a Jump piece and place it on an ice patch
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var board = new Board();

        // Manually place an ice patch at (3, 3)
        board.PlaceIcePatch(new Position(3, 3));

        // Create a Jump piece for P1 (Goofy)
        var jumpPiece = new Piece(
            Guid.NewGuid(), "Goofy", p1,
            EntryPointType.Corners, MovementType.Jump, 3, 1);
        var p1Fillers = Enumerable.Range(0, 4)
            .Select(i => new Piece($"Fill{i}", p1, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var p2Piece = new Piece("P2", p2, EntryPointType.Borders, MovementType.Orthogonal, 1, 1);
        var p2Fillers = Enumerable.Range(0, 4)
            .Select(i => new Piece($"Fill{i}", p2, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var game = new Game(p1, p2, board);
        game.SetLineup(p1, new Lineup(new List<Piece> { jumpPiece }.Concat(p1Fillers).ToList()));
        game.SetLineup(p2, new Lineup(new List<Piece> { p2Piece }.Concat(p2Fillers).ToList()));
        game.Start();

        game.AdvancePhase(); // CoinSpawn → PlacePhase
        game.PlacePiece(p1, jumpPiece.Id, new Position(0, 0));
        game.PlacePiece(p2, p2Piece.Id, new Position(7, 7));

        // Act: Jump piece jumps to (3, 3) which has an ice patch
        var segment = (IReadOnlyList<Position>)new List<Position> { new(3, 3) }.AsReadOnly();
        var segments = new List<IReadOnlyList<Position>> { segment }.AsReadOnly();

        game.MovePiece(p1, jumpPiece.Id, segments);

        // Assert: piece ends at (3, 3), not slid anywhere
        Assert.Equal(new Position(3, 3), jumpPiece.Position);
    }

    [Fact]
    public void IcePatches_PersistAcrossTurns()
    {
        // Arrange: Elsa places ice patches
        var (game, p1, _) = CreateGameInMovePhase();

        // Act: Move Elsa to place ice patches
        var segment = (IReadOnlyList<Position>)new List<Position> 
        { 
            new(0, 1),
            new(0, 2),
            new(0, 3)
        }.AsReadOnly();
        var segments = new List<IReadOnlyList<Position>> { segment }.AsReadOnly();

        var elsaPiece = game.LineupPlayerOne!.Pieces[0];
        game.MovePiece(p1, elsaPiece.Id, segments);

        var icePatches = game.Board.GetIcePatches();
        Assert.Equal(2, icePatches.Count);
        Assert.Contains(new Position(0, 1), icePatches);
        Assert.Contains(new Position(0, 2), icePatches);

        // Assert: Ice patches should remain
        Assert.True(game.Board.HasIcePatch(new Position(0, 1)));
        Assert.True(game.Board.HasIcePatch(new Position(0, 2)));
    }
}
