using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.Tests.Helpers;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Tests;

public class LineupTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Creates N distinct valid pieces, each owned by a different player.</summary>
    private static List<Piece> MakePieces(int count) =>
        Enumerable.Range(0, count)
            .Select(i => PieceFactory.Any($"Piece{i}"))
            .ToList();

    // ── Valid construction ────────────────────────────────────────────────────

    [Fact]
    public void Constructor_WithExactlyFiveUniquePieces_Succeeds()
    {
        var pieces = MakePieces(5);

        var lineup = new Lineup(pieces);

        Assert.NotNull(lineup);
    }

    [Fact]
    public void Constructor_WithExactlyFivePieces_PiecesHasFiveItems()
    {
        var pieces = MakePieces(5);

        var lineup = new Lineup(pieces);

        Assert.Equal(5, lineup.Pieces.Count);
    }

    [Fact]
    public void Constructor_PreservesOrderOfPieces()
    {
        var pieces = MakePieces(5);

        var lineup = new Lineup(pieces);

        for (var i = 0; i < 5; i++)
            Assert.Equal(pieces[i].Id, lineup.Pieces[i].Id);
    }

    // ── Null collection ───────────────────────────────────────────────────────

    [Fact]
    public void Constructor_WithNullCollection_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new Lineup(null!));
    }

    // ── Wrong piece count ─────────────────────────────────────────────────────

    [Fact]
    public void Constructor_WithFourPieces_ThrowsDomainException()
    {
        var pieces = MakePieces(4);

        Assert.Throws<DomainException>(() => new Lineup(pieces));
    }

    [Fact]
    public void Constructor_WithSixPieces_ThrowsDomainException()
    {
        var pieces = MakePieces(6);

        Assert.Throws<DomainException>(() => new Lineup(pieces));
    }

    [Fact]
    public void Constructor_WithZeroPieces_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new Lineup(Enumerable.Empty<Piece>()));
    }

    // ── Null pieces in the collection ─────────────────────────────────────────

    [Fact]
    public void Constructor_WithFirstPieceNull_ThrowsDomainException()
    {
        var pieces = MakePieces(5);
        pieces[0] = null!;

        Assert.Throws<DomainException>(() => new Lineup(pieces));
    }

    [Fact]
    public void Constructor_WithMiddlePieceNull_ThrowsDomainException()
    {
        var pieces = MakePieces(5);
        pieces[2] = null!;

        Assert.Throws<DomainException>(() => new Lineup(pieces));
    }

    [Fact]
    public void Constructor_WithLastPieceNull_ThrowsDomainException()
    {
        var pieces = MakePieces(5);
        pieces[4] = null!;

        Assert.Throws<DomainException>(() => new Lineup(pieces));
    }

    // ── Duplicate piece IDs ───────────────────────────────────────────────────

    [Fact]
    public void Constructor_WithDuplicatePieceIds_ThrowsDomainException()
    {
        var pieces = MakePieces(4);
        // Add the first piece again — same Id, same reference
        pieces.Add(pieces[0]);

        Assert.Throws<DomainException>(() => new Lineup(pieces));
    }

    [Fact]
    public void Constructor_WithAllSamePiece_ThrowsDomainException()
    {
        var single = PieceFactory.Any("Duplicate");
        var pieces = Enumerable.Repeat(single, 5).ToList();

        Assert.Throws<DomainException>(() => new Lineup(pieces));
    }

    // ── Equals (order-dependent, ID-based) ───────────────────────────────────

    [Fact]
    public void Equals_SameFivePiecesInSameOrder_ReturnsTrue()
    {
        var pieces = MakePieces(5);
        var lineup1 = new Lineup(pieces);
        var lineup2 = new Lineup(pieces);

        Assert.True(lineup1.Equals(lineup2));
    }

    [Fact]
    public void Equals_SameFivePiecesInDifferentOrder_ReturnsFalse()
    {
        var pieces = MakePieces(5);
        var lineup1 = new Lineup(pieces);

        // Reverse the list to guarantee a different order
        var reordered = pieces.AsEnumerable().Reverse().ToList();
        var lineup2 = new Lineup(reordered);

        Assert.False(lineup1.Equals(lineup2));
    }

    [Fact]
    public void Equals_WithNull_ReturnsFalse()
    {
        var lineup = new Lineup(MakePieces(5));

        Assert.False(lineup.Equals(null));
    }

    [Fact]
    public void Equals_WithSelf_ReturnsTrue()
    {
        var lineup = new Lineup(MakePieces(5));

        Assert.True(lineup.Equals(lineup));
    }

    [Fact]
    public void EqualsObject_WithEqualLineup_ReturnsTrue()
    {
        var pieces = MakePieces(5);
        var lineup1 = new Lineup(pieces);
        var lineup2 = new Lineup(pieces);

        Assert.True(lineup1.Equals((object)lineup2));
    }

    [Fact]
    public void EqualsObject_WithNonLineupObject_ReturnsFalse()
    {
        var lineup = new Lineup(MakePieces(5));
        var notALineUp = new object();
        Assert.False(lineup.Equals(notALineUp));
    }

    // ── Equality operators ────────────────────────────────────────────────────

    [Fact]
    public void EqualityOperator_ForEqualLineups_ReturnsTrue()
    {
        var pieces = MakePieces(5);
        var lineup1 = new Lineup(pieces);
        var lineup2 = new Lineup(pieces);

        Assert.True(lineup1 == lineup2);
    }

    [Fact]
    public void EqualityOperator_ForDifferentLineups_ReturnsFalse()
    {
        var pieces = MakePieces(5);
        var lineup1 = new Lineup(pieces);
        var lineup2 = new Lineup(pieces.AsEnumerable().Reverse().ToList());

        Assert.False(lineup1 == lineup2);
    }

    [Fact]
    public void InequalityOperator_ForDifferentLineups_ReturnsTrue()
    {
        var pieces = MakePieces(5);
        var lineup1 = new Lineup(pieces);
        var lineup2 = new Lineup(pieces.AsEnumerable().Reverse().ToList());

        Assert.True(lineup1 != lineup2);
    }

    [Fact]
    public void InequalityOperator_ForEqualLineups_ReturnsFalse()
    {
        var pieces = MakePieces(5);
        var lineup1 = new Lineup(pieces);
        var lineup2 = new Lineup(pieces);

        Assert.False(lineup1 != lineup2);
    }

    [Fact]
    public void EqualityOperator_BothNull_ReturnsTrue()
    {
        Lineup? left = null;
        Lineup? right = null;

        Assert.True(left == right);
    }

    [Fact]
    public void EqualityOperator_LeftNull_ReturnsFalse()
    {
        Lineup? left = null;
        var right = new Lineup(MakePieces(5));

        Assert.False(left == right);
    }

    [Fact]
    public void EqualityOperator_RightNull_ReturnsFalse()
    {
        var left = new Lineup(MakePieces(5));
        Lineup? right = null;

        Assert.False(left == right);
    }

    // ── GetHashCode ───────────────────────────────────────────────────────────

    [Fact]
    public void GetHashCode_ForEqualLineups_IsConsistent()
    {
        var pieces = MakePieces(5);
        var lineup1 = new Lineup(pieces);
        var lineup2 = new Lineup(pieces);

        Assert.Equal(lineup1.GetHashCode(), lineup2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_CalledTwiceOnSameInstance_ReturnsSameValue()
    {
        var lineup = new Lineup(MakePieces(5));

        Assert.Equal(lineup.GetHashCode(), lineup.GetHashCode());
    }

    // ── Pieces are read-only ───────────────────────────────────────────────────

    [Fact]
    public void Pieces_PropertyType_IsIReadOnlyList()
    {
        var lineup = new Lineup(MakePieces(5));

        Assert.IsAssignableFrom<IReadOnlyList<Piece>>(lineup.Pieces);
    }

    [Fact]
    public void Pieces_IsNotDirectlyCastableToList_SoCannotBeModifiedByCallers()
    {
        var lineup = new Lineup(MakePieces(5));

        // AsReadOnly() returns a ReadOnlyCollection<T>, not a List<T>.
        // Mutating operations (Add, Remove) throw NotSupportedException at runtime,
        // and the type is not directly castable to List<Piece>.
        var castSucceeded = lineup.Pieces is List<Piece>;
        Assert.False(castSucceeded);
    }
}
