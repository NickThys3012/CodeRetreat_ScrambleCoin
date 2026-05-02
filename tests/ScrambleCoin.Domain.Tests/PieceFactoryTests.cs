using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.Factories;

namespace ScrambleCoin.Domain.Tests;

/// <summary>
/// Unit tests for <see cref="PieceFactory"/> (Issue #37).
/// </summary>
public class PieceFactoryTests
{
    private static readonly Guid AnyPlayerId = Guid.NewGuid();

    // ── Happy-path tests ──────────────────────────────────────────────────────

    [Fact]
    public void Create_Mickey_ReturnsOrthogonalPiece()
    {
        // Act
        var piece = PieceFactory.Create("Mickey", AnyPlayerId);

        // Assert: Mickey is an orthogonal mover.
        Assert.Equal(MovementType.Orthogonal, piece.MovementType);
    }

    [Fact]
    public void Create_Mickey_HasMaxDistanceThree()
    {
        var piece = PieceFactory.Create("Mickey", AnyPlayerId);

        Assert.Equal(3, piece.MaxDistance);
    }

    [Fact]
    public void Create_Mickey_HasBordersEntryPoint()
    {
        var piece = PieceFactory.Create("Mickey", AnyPlayerId);

        Assert.Equal(EntryPointType.Borders, piece.EntryPointType);
    }

    [Fact]
    public void Create_Minnie_ReturnsDiagonalPiece()
    {
        var piece = PieceFactory.Create("Minnie", AnyPlayerId);

        Assert.Equal(MovementType.Diagonal, piece.MovementType);
    }

    [Fact]
    public void Create_Minnie_HasBordersEntryPoint()
    {
        var piece = PieceFactory.Create("Minnie", AnyPlayerId);

        Assert.Equal(EntryPointType.Borders, piece.EntryPointType);
    }

    [Fact]
    public void Create_Donald_ReturnsAnyDirectionPiece()
    {
        var piece = PieceFactory.Create("Donald", AnyPlayerId);

        Assert.Equal(MovementType.AnyDirection, piece.MovementType);
    }

    [Fact]
    public void Create_Donald_HasCornersEntryPoint()
    {
        var piece = PieceFactory.Create("Donald", AnyPlayerId);

        Assert.Equal(EntryPointType.Corners, piece.EntryPointType);
    }

    [Fact]
    public void Create_Goofy_ReturnsAnyDirectionPiece()
    {
        var piece = PieceFactory.Create("Goofy", AnyPlayerId);

        Assert.Equal(MovementType.AnyDirection, piece.MovementType);
    }

    [Fact]
    public void Create_Goofy_HasCornersEntryPoint()
    {
        var piece = PieceFactory.Create("Goofy", AnyPlayerId);

        Assert.Equal(EntryPointType.Corners, piece.EntryPointType);
    }

    [Fact]
    public void Create_Scrooge_HasMaxDistanceTwo()
    {
        var piece = PieceFactory.Create("Scrooge", AnyPlayerId);

        // Scrooge is limited to 2 tiles.
        Assert.Equal(2, piece.MaxDistance);
    }

    [Fact]
    public void Create_Scrooge_HasCornersEntryPoint()
    {
        var piece = PieceFactory.Create("Scrooge", AnyPlayerId);

        Assert.Equal(EntryPointType.Corners, piece.EntryPointType);
    }

    // ── Parameterised: all starter pieces return a non-null Piece ────────────

    public static IEnumerable<object[]> StarterPieceNames =>
        new List<object[]>
        {
            new object[] { "Mickey"  },
            new object[] { "Minnie"  },
            new object[] { "Donald"  },
            new object[] { "Goofy"   },
            new object[] { "Scrooge" },
        };

    [Theory]
    [MemberData(nameof(StarterPieceNames))]
    public void Create_AllStarterPieces_ReturnsPiece(string pieceName)
    {
        // Act
        var piece = PieceFactory.Create(pieceName, AnyPlayerId);

        // Assert: a piece is returned with the correct name and owner.
        Assert.NotNull(piece);
        Assert.Equal(pieceName, piece.Name, ignoreCase: true);
        Assert.Equal(AnyPlayerId, piece.PlayerId);
    }

    [Theory]
    [MemberData(nameof(StarterPieceNames))]
    public void Create_AllStarterPieces_HaveMovesPerTurnOfOne(string pieceName)
    {
        var piece = PieceFactory.Create(pieceName, AnyPlayerId);

        Assert.Equal(1, piece.MovesPerTurn);
    }

    [Theory]
    [MemberData(nameof(StarterPieceNames))]
    public void Create_AllStarterPieces_ReturnUniqueIds(string pieceName)
    {
        // Each call should produce a fresh piece ID.
        var piece1 = PieceFactory.Create(pieceName, AnyPlayerId);
        var piece2 = PieceFactory.Create(pieceName, AnyPlayerId);

        Assert.NotEqual(piece1.Id, piece2.Id);
    }

    // ── Error-path tests ──────────────────────────────────────────────────────

    [Fact]
    public void Create_UnknownPiece_ThrowsDomainException()
    {
        // Act & Assert
        Assert.Throws<DomainException>(() =>
            PieceFactory.Create("Pluto", AnyPlayerId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("pluto")]
    [InlineData("UNKNOWN_PIECE")]
    [InlineData("Daisy")]
    public void Create_UnrecognisedPieceName_ThrowsDomainException(string pieceName)
    {
        Assert.Throws<DomainException>(() =>
            PieceFactory.Create(pieceName, AnyPlayerId));
    }

    // ── Case-insensitivity ────────────────────────────────────────────────────

    [Theory]
    [InlineData("mickey")]
    [InlineData("MICKEY")]
    [InlineData("MiCkEy")]
    public void Create_CaseInsensitivePieceName_ReturnsPiece(string pieceName)
    {
        // Factory is documented as case-insensitive.
        var piece = PieceFactory.Create(pieceName, AnyPlayerId);

        Assert.NotNull(piece);
        Assert.Equal(MovementType.Orthogonal, piece.MovementType);
    }
}
