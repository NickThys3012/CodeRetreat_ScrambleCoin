using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Tests;

public class PositionTests
{
    // ── Construction ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_WithMinValues_ShouldSucceed()
    {
        var pos = new Position(0, 0);
        Assert.Equal(0, pos.Row);
        Assert.Equal(0, pos.Col);
    }

    [Fact]
    public void Constructor_WithMaxValues_ShouldSucceed()
    {
        var pos = new Position(7, 7);
        Assert.Equal(7, pos.Row);
        Assert.Equal(7, pos.Col);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(8, 0)]
    [InlineData(0, 8)]
    [InlineData(-1, -1)]
    [InlineData(8, 8)]
    public void Constructor_WithOutOfBoundsValues_ShouldThrowDomainException(int row, int col)
    {
        Assert.Throws<DomainException>(() => new Position(row, col));
    }

    // ── Equality ──────────────────────────────────────────────────────────────

    [Fact]
    public void Equals_SameRowAndCol_ShouldBeEqual()
    {
        var a = new Position(3, 4);
        var b = new Position(3, 4);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equals_DifferentRow_ShouldNotBeEqual()
    {
        var a = new Position(2, 4);
        var b = new Position(3, 4);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equals_DifferentCol_ShouldNotBeEqual()
    {
        var a = new Position(3, 3);
        var b = new Position(3, 4);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void EqualityOperator_SameValues_ShouldBeTrue()
    {
        var a = new Position(1, 2);
        var b = new Position(1, 2);
        Assert.True(a == b);
    }

    [Fact]
    public void InequalityOperator_DifferentValues_ShouldBeTrue()
    {
        var a = new Position(1, 2);
        var b = new Position(2, 1);
        Assert.True(a != b);
    }

    [Fact]
    public void GetHashCode_SameValues_ShouldBeEqual()
    {
        var a = new Position(5, 6);
        var b = new Position(5, 6);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    // ── IsOrthogonallyAdjacentTo ──────────────────────────────────────────────

    [Fact]
    public void IsOrthogonallyAdjacentTo_SameRowColPlusOne_ShouldBeTrue()
    {
        var a = new Position(3, 3);
        var b = new Position(3, 4);
        Assert.True(a.IsOrthogonallyAdjacentTo(b));
    }

    [Fact]
    public void IsOrthogonallyAdjacentTo_SameRowColMinusOne_ShouldBeTrue()
    {
        var a = new Position(3, 3);
        var b = new Position(3, 2);
        Assert.True(a.IsOrthogonallyAdjacentTo(b));
    }

    [Fact]
    public void IsOrthogonallyAdjacentTo_SameColRowPlusOne_ShouldBeTrue()
    {
        var a = new Position(3, 3);
        var b = new Position(4, 3);
        Assert.True(a.IsOrthogonallyAdjacentTo(b));
    }

    [Fact]
    public void IsOrthogonallyAdjacentTo_SameColRowMinusOne_ShouldBeTrue()
    {
        var a = new Position(3, 3);
        var b = new Position(2, 3);
        Assert.True(a.IsOrthogonallyAdjacentTo(b));
    }

    [Fact]
    public void IsOrthogonallyAdjacentTo_Diagonal_ShouldBeFalse()
    {
        var a = new Position(3, 3);
        var b = new Position(4, 4);
        Assert.False(a.IsOrthogonallyAdjacentTo(b));
    }

    [Fact]
    public void IsOrthogonallyAdjacentTo_SameTile_ShouldBeFalse()
    {
        var a = new Position(3, 3);
        var b = new Position(3, 3);
        Assert.False(a.IsOrthogonallyAdjacentTo(b));
    }

    [Fact]
    public void IsOrthogonallyAdjacentTo_TwoApartSameRow_ShouldBeFalse()
    {
        var a = new Position(3, 3);
        var b = new Position(3, 5);
        Assert.False(a.IsOrthogonallyAdjacentTo(b));
    }

    [Fact]
    public void IsOrthogonallyAdjacentTo_TwoApartSameCol_ShouldBeFalse()
    {
        var a = new Position(3, 3);
        var b = new Position(5, 3);
        Assert.False(a.IsOrthogonallyAdjacentTo(b));
    }

    // ── IsDiagonallyAdjacentTo ────────────────────────────────────────────────

    [Fact]
    public void IsDiagonallyAdjacentTo_PlusOneRowPlusOneCol_ShouldBeTrue()
    {
        var a = new Position(3, 3);
        var b = new Position(4, 4);
        Assert.True(a.IsDiagonallyAdjacentTo(b));
    }

    [Fact]
    public void IsDiagonallyAdjacentTo_PlusOneRowMinusOneCol_ShouldBeTrue()
    {
        var a = new Position(3, 3);
        var b = new Position(4, 2);
        Assert.True(a.IsDiagonallyAdjacentTo(b));
    }

    [Fact]
    public void IsDiagonallyAdjacentTo_MinusOneRowPlusOneCol_ShouldBeTrue()
    {
        var a = new Position(3, 3);
        var b = new Position(2, 4);
        Assert.True(a.IsDiagonallyAdjacentTo(b));
    }

    [Fact]
    public void IsDiagonallyAdjacentTo_MinusOneRowMinusOneCol_ShouldBeTrue()
    {
        var a = new Position(3, 3);
        var b = new Position(2, 2);
        Assert.True(a.IsDiagonallyAdjacentTo(b));
    }

    [Fact]
    public void IsDiagonallyAdjacentTo_OrthogonalNeighbour_ShouldBeFalse()
    {
        var a = new Position(3, 3);
        var b = new Position(3, 4);
        Assert.False(a.IsDiagonallyAdjacentTo(b));
    }

    [Fact]
    public void IsDiagonallyAdjacentTo_SameTile_ShouldBeFalse()
    {
        var a = new Position(3, 3);
        Assert.False(a.IsDiagonallyAdjacentTo(a));
    }

    [Fact]
    public void IsDiagonallyAdjacentTo_TwoApartDiagonal_ShouldBeFalse()
    {
        var a = new Position(1, 1);
        var b = new Position(3, 3);
        Assert.False(a.IsDiagonallyAdjacentTo(b));
    }
}
