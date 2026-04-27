using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.Obstacles;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Tests;

public class RockTests
{
    [Fact]
    public void Rock_Constructor_ShouldStorePosition()
    {
        var pos = new Position(2, 3);
        var rock = new Rock(pos);
        Assert.Equal(pos, rock.Position);
    }
}

public class LakeTests
{
    [Fact]
    public void Lake_WithValidTopLeft_ShouldCoverFourTiles()
    {
        var lake = new Lake(new Position(2, 2));
        Assert.Equal(4, lake.CoveredPositions.Count);
    }

    [Fact]
    public void Lake_WithTopLeftAt2_2_ShouldCoverTopLeft()
    {
        var lake = new Lake(new Position(2, 2));
        Assert.Contains(lake.CoveredPositions, p => p == new Position(2, 2));
    }

    [Fact]
    public void Lake_WithTopLeftAt2_2_ShouldCoverTopRight()
    {
        var lake = new Lake(new Position(2, 2));
        Assert.Contains(lake.CoveredPositions, p => p == new Position(2, 3));
    }

    [Fact]
    public void Lake_WithTopLeftAt2_2_ShouldCoverBottomLeft()
    {
        var lake = new Lake(new Position(2, 2));
        Assert.Contains(lake.CoveredPositions, p => p == new Position(3, 2));
    }

    [Fact]
    public void Lake_WithTopLeftAt2_2_ShouldCoverBottomRight()
    {
        var lake = new Lake(new Position(2, 2));
        Assert.Contains(lake.CoveredPositions, p => p == new Position(3, 3));
    }

    [Fact]
    public void Lake_Covers_PositionInsideLake_ShouldReturnTrue()
    {
        var lake = new Lake(new Position(2, 2));
        Assert.True(lake.Covers(new Position(2, 2)));
        Assert.True(lake.Covers(new Position(2, 3)));
        Assert.True(lake.Covers(new Position(3, 2)));
        Assert.True(lake.Covers(new Position(3, 3)));
    }

    [Fact]
    public void Lake_Covers_PositionOutsideLake_ShouldReturnFalse()
    {
        var lake = new Lake(new Position(2, 2));
        Assert.False(lake.Covers(new Position(1, 2)));
        Assert.False(lake.Covers(new Position(4, 2)));
        Assert.False(lake.Covers(new Position(2, 1)));
        Assert.False(lake.Covers(new Position(2, 4)));
    }

    [Fact]
    public void Lake_WithTopLeftAtRow7_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() => new Lake(new Position(7, 0)));
    }

    [Fact]
    public void Lake_WithTopLeftAtCol7_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() => new Lake(new Position(0, 7)));
    }

    [Fact]
    public void Lake_WithTopLeftAt7_7_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() => new Lake(new Position(7, 7)));
    }

    [Fact]
    public void Lake_WithTopLeftAt7_6_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() => new Lake(new Position(7, 6)));
    }

    [Fact]
    public void Lake_WithTopLeftAt6_7_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() => new Lake(new Position(6, 7)));
    }

    [Fact]
    public void Lake_WithTopLeftAt6_6_ShouldBeValid()
    {
        // (6,6), (6,7), (7,6), (7,7) — all within board bounds
        var lake = new Lake(new Position(6, 6));
        Assert.Equal(4, lake.CoveredPositions.Count);
    }
}

public class FenceTests
{
    [Fact]
    public void Fence_Constructor_WithAdjacentPositions_ShouldSucceed()
    {
        var a = new Position(3, 3);
        var b = new Position(3, 4);
        var fence = new Fence(a, b);
        Assert.Equal(a, fence.From);
        Assert.Equal(b, fence.To);
    }

    [Fact]
    public void Fence_Constructor_WithNonAdjacentPositions_ShouldThrowDomainException()
    {
        var a = new Position(3, 3);
        var b = new Position(5, 3);
        Assert.Throws<DomainException>(() => new Fence(a, b));
    }

    [Fact]
    public void Fence_Constructor_WithDiagonalPositions_ShouldThrowDomainException()
    {
        var a = new Position(3, 3);
        var b = new Position(4, 4);
        Assert.Throws<DomainException>(() => new Fence(a, b));
    }

    [Fact]
    public void Fence_Constructor_WithSamePosition_ShouldThrowDomainException()
    {
        var a = new Position(3, 3);
        Assert.Throws<DomainException>(() => new Fence(a, a));
    }

    [Fact]
    public void Fence_IsOnEdge_WithMatchingPositions_ShouldReturnTrue()
    {
        var a = new Position(1, 1);
        var b = new Position(1, 2);
        var fence = new Fence(a, b);
        Assert.True(fence.IsOnEdge(a, b));
    }

    [Fact]
    public void Fence_IsOnEdge_WithReversedPositions_ShouldReturnTrue()
    {
        var a = new Position(1, 1);
        var b = new Position(1, 2);
        var fence = new Fence(a, b);
        Assert.True(fence.IsOnEdge(b, a));
    }

    [Fact]
    public void Fence_IsOnEdge_WithDifferentEdge_ShouldReturnFalse()
    {
        var a = new Position(1, 1);
        var b = new Position(1, 2);
        var fence = new Fence(a, b);
        var c = new Position(2, 1);
        var d = new Position(2, 2);
        Assert.False(fence.IsOnEdge(c, d));
    }
}
