using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.Tests.Helpers;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Tests;

public class PieceTests
{
    // ── Construction (explicit-Id constructor) ────────────────────────────────

    [Fact]
    public void Constructor_WithExplicitId_StoresIdCorrectly()
    {
        var id = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var piece = new Piece(id, "Mickey Mouse", playerId,
            EntryPointType.Borders, MovementType.Orthogonal, 3, 1);

        Assert.Equal(id, piece.Id);
    }

    [Fact]
    public void Constructor_StoresNameCorrectly()
    {
        var piece = new Piece(Guid.NewGuid(), "Mickey Mouse", Guid.NewGuid(),
            EntryPointType.Borders, MovementType.Orthogonal, 3, 1);

        Assert.Equal("Mickey Mouse", piece.Name);
    }

    [Fact]
    public void Constructor_StoresPlayerIdCorrectly()
    {
        var playerId = Guid.NewGuid();
        var piece = new Piece(Guid.NewGuid(), "Mickey Mouse", playerId,
            EntryPointType.Borders, MovementType.Orthogonal, 3, 1);

        Assert.Equal(playerId, piece.PlayerId);
    }

    [Fact]
    public void Constructor_StoresEntryPointTypeCorrectly()
    {
        var piece = new Piece(Guid.NewGuid(), "Donald Duck", Guid.NewGuid(),
            EntryPointType.Corners, MovementType.Orthogonal, 3, 1);

        Assert.Equal(EntryPointType.Corners, piece.EntryPointType);
    }

    [Fact]
    public void Constructor_StoresMovementTypeCorrectly()
    {
        var piece = new Piece(Guid.NewGuid(), "Donald Duck", Guid.NewGuid(),
            EntryPointType.Corners, MovementType.Diagonal, 3, 1);

        Assert.Equal(MovementType.Diagonal, piece.MovementType);
    }

    [Fact]
    public void Constructor_StoresMaxDistanceCorrectly()
    {
        var piece = new Piece(Guid.NewGuid(), "Mickey Mouse", Guid.NewGuid(),
            EntryPointType.Borders, MovementType.Orthogonal, 3, 1);

        Assert.Equal(3, piece.MaxDistance);
    }

    [Fact]
    public void Constructor_StoresMovesPerTurnCorrectly()
    {
        var piece = new Piece(Guid.NewGuid(), "Mickey Mouse", Guid.NewGuid(),
            EntryPointType.Borders, MovementType.Orthogonal, 3, 1);

        Assert.Equal(1, piece.MovesPerTurn);
    }

    // ── Auto-generated Id constructor ─────────────────────────────────────────

    [Fact]
    public void AutoIdConstructor_GivesEachPieceAUniqueId()
    {
        var playerId = Guid.NewGuid();
        var piece1 = new Piece("Piece A", playerId, EntryPointType.Borders, MovementType.Orthogonal, 3, 1);
        var piece2 = new Piece("Piece B", playerId, EntryPointType.Borders, MovementType.Orthogonal, 3, 1);

        Assert.NotEqual(piece1.Id, piece2.Id);
    }

    [Fact]
    public void AutoIdConstructor_GeneratesNonEmptyId()
    {
        var piece = new Piece("Piece A", Guid.NewGuid(), EntryPointType.Borders, MovementType.Orthogonal, 3, 1);

        Assert.NotEqual(Guid.Empty, piece.Id);
    }

    // ── Validation: name ──────────────────────────────────────────────────────

    [Fact]
    public void Constructor_WithNullName_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            new Piece(Guid.NewGuid(), null!, Guid.NewGuid(),
                EntryPointType.Borders, MovementType.Orthogonal, 3, 1));

        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    [Fact]
    public void Constructor_WithWhitespaceName_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() =>
            new Piece(Guid.NewGuid(), "   ", Guid.NewGuid(),
                EntryPointType.Borders, MovementType.Orthogonal, 3, 1));
    }

    [Fact]
    public void Constructor_WithEmptyName_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() =>
            new Piece(Guid.NewGuid(), string.Empty, Guid.NewGuid(),
                EntryPointType.Borders, MovementType.Orthogonal, 3, 1));
    }

    // ── Validation: maxDistance ───────────────────────────────────────────────

    [Fact]
    public void Constructor_WithMaxDistanceZero_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() =>
            new Piece(Guid.NewGuid(), "Mickey Mouse", Guid.NewGuid(),
                EntryPointType.Borders, MovementType.Orthogonal, maxDistance: 0, movesPerTurn: 1));
    }

    [Fact]
    public void Constructor_WithMaxDistanceNegative_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() =>
            new Piece(Guid.NewGuid(), "Mickey Mouse", Guid.NewGuid(),
                EntryPointType.Borders, MovementType.Orthogonal, maxDistance: -1, movesPerTurn: 1));
    }

    [Fact]
    public void Constructor_WithMaxDistanceOne_Succeeds()
    {
        var piece = new Piece(Guid.NewGuid(), "Anna", Guid.NewGuid(),
            EntryPointType.Borders, MovementType.Orthogonal, maxDistance: 1, movesPerTurn: 1);

        Assert.Equal(1, piece.MaxDistance);
    }

    // ── Validation: movesPerTurn ──────────────────────────────────────────────

    [Fact]
    public void Constructor_WithMovesPerTurnZero_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() =>
            new Piece(Guid.NewGuid(), "Mickey Mouse", Guid.NewGuid(),
                EntryPointType.Borders, MovementType.Orthogonal, maxDistance: 3, movesPerTurn: 0));
    }

    [Fact]
    public void Constructor_WithMovesPerTurnNegative_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() =>
            new Piece(Guid.NewGuid(), "Mickey Mouse", Guid.NewGuid(),
                EntryPointType.Borders, MovementType.Orthogonal, maxDistance: 3, movesPerTurn: -1));
    }

    // ── MovesPerTurn > 1 (Anna-style piece) ───────────────────────────────────

    [Fact]
    public void Constructor_WithMovesPerTurnGreaterThanOne_StoresValueCorrectly()
    {
        var piece = new Piece(Guid.NewGuid(), "Anna", Guid.NewGuid(),
            EntryPointType.Borders, MovementType.Orthogonal, maxDistance: 1, movesPerTurn: 3);

        Assert.Equal(3, piece.MovesPerTurn);
    }

    [Fact]
    public void Constructor_AnnaStylePiece_HasMaxDistanceOne()
    {
        var piece = new Piece(Guid.NewGuid(), "Anna", Guid.NewGuid(),
            EntryPointType.Borders, MovementType.Orthogonal, maxDistance: 1, movesPerTurn: 3);

        Assert.Equal(1, piece.MaxDistance);
    }

    // ── EntryPointType values ─────────────────────────────────────────────────

    [Fact]
    public void Constructor_WithEntryPointTypeBorders_CanBeReadBack()
    {
        var piece = new Piece(Guid.NewGuid(), "TestPiece", Guid.NewGuid(),
            EntryPointType.Borders, MovementType.Orthogonal, 3, 1);

        Assert.Equal(EntryPointType.Borders, piece.EntryPointType);
    }

    [Fact]
    public void Constructor_WithEntryPointTypeCorners_CanBeReadBack()
    {
        var piece = new Piece(Guid.NewGuid(), "Donald Duck", Guid.NewGuid(),
            EntryPointType.Corners, MovementType.Orthogonal, 3, 1);

        Assert.Equal(EntryPointType.Corners, piece.EntryPointType);
    }

    [Fact]
    public void Constructor_WithEntryPointTypeAnywhere_CanBeReadBack()
    {
        var piece = new Piece(Guid.NewGuid(), "Tinker Bell", Guid.NewGuid(),
            EntryPointType.Anywhere, MovementType.Orthogonal, 3, 1);

        Assert.Equal(EntryPointType.Anywhere, piece.EntryPointType);
    }

    // ── MovementType values ───────────────────────────────────────────────────

    [Fact]
    public void Constructor_WithMovementTypeOrthogonal_CanBeReadBack()
    {
        var piece = new Piece(Guid.NewGuid(), "TestPiece", Guid.NewGuid(),
            EntryPointType.Borders, MovementType.Orthogonal, 3, 1);

        Assert.Equal(MovementType.Orthogonal, piece.MovementType);
    }

    [Fact]
    public void Constructor_WithMovementTypeDiagonal_CanBeReadBack()
    {
        var piece = new Piece(Guid.NewGuid(), "Goofy", Guid.NewGuid(),
            EntryPointType.Borders, MovementType.Diagonal, 3, 1);

        Assert.Equal(MovementType.Diagonal, piece.MovementType);
    }

    [Fact]
    public void Constructor_WithMovementTypeAnyDirection_CanBeReadBack()
    {
        var piece = new Piece(Guid.NewGuid(), "Donald Duck", Guid.NewGuid(),
            EntryPointType.Corners, MovementType.AnyDirection, 3, 1);

        Assert.Equal(MovementType.AnyDirection, piece.MovementType);
    }

    // ── IsOnBoard ─────────────────────────────────────────────────────────────

    [Fact]
    public void Position_BeforePlaceAt_IsNull()
    {
        var piece = PieceFactory.Any();

        Assert.Null(piece.Position);
    }

    [Fact]
    public void IsOnBoard_BeforePlaceAt_IsFalse()
    {
        var piece = PieceFactory.Any();

        Assert.False(piece.IsOnBoard);
    }

    [Fact]
    public void IsOnBoard_AfterPlaceAt_IsTrue()
    {
        var piece = PieceFactory.Any();
        piece.PlaceAt(new Position(3, 4));

        Assert.True(piece.IsOnBoard);
    }

    [Fact]
    public void IsOnBoard_AfterRemoveFromBoard_IsFalse()
    {
        var piece = PieceFactory.Any();
        piece.PlaceAt(new Position(3, 4));
        piece.RemoveFromBoard();

        Assert.False(piece.IsOnBoard);
    }

    // ── PlaceAt ───────────────────────────────────────────────────────────────

    [Fact]
    public void PlaceAt_SetsPositionCorrectly()
    {
        var piece = PieceFactory.Any();
        var position = new Position(2, 5);
        piece.PlaceAt(position);

        Assert.Equal(position, piece.Position);
    }

    [Fact]
    public void PlaceAt_WithNullPosition_ThrowsDomainException()
    {
        var piece = PieceFactory.Any();

        Assert.Throws<DomainException>(() => piece.PlaceAt(null!));
    }

    [Fact]
    public void PlaceAt_CalledTwice_UpdatesPositionToLatest()
    {
        var piece = PieceFactory.Any();
        piece.PlaceAt(new Position(0, 0));
        piece.PlaceAt(new Position(7, 7));

        Assert.Equal(new Position(7, 7), piece.Position);
    }

    // ── RemoveFromBoard ───────────────────────────────────────────────────────

    [Fact]
    public void RemoveFromBoard_SetsPositionToNull()
    {
        var piece = PieceFactory.Any();
        piece.PlaceAt(new Position(4, 4));
        piece.RemoveFromBoard();

        Assert.Null(piece.Position);
    }

    [Fact]
    public void RemoveFromBoard_WhenNotOnBoard_DoesNotThrow()
    {
        var piece = PieceFactory.Any();

        // should be a no-op without throwing
        var ex = Record.Exception(() => piece.RemoveFromBoard());
        Assert.Null(ex);
    }
}
