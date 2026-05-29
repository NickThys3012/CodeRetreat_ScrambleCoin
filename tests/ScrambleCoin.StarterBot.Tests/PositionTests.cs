using ScrambleCoin.StarterBot.Models;

namespace ScrambleCoin.StarterBot.Tests;

/// <summary>
/// Tests for the <see cref="Position"/> value type helper methods.
/// No dependencies on infrastructure or HTTP — pure math only.
/// </summary>
public sealed class PositionTests
{
    // ── DistanceTo ─────────────────────────────────────────────────────────────

    [Fact]
    public void DistanceTo_SamePosition_ReturnsZero()
    {
        var p = new Position(3, 4);

        var distance = p.DistanceTo(new Position(3, 4));

        Assert.Equal(0.0, distance);
    }

    [Fact]
    public void DistanceTo_HorizontalNeighbour_ReturnsOne()
    {
        var from = new Position(0, 0);
        var to   = new Position(0, 1);

        Assert.Equal(1.0, from.DistanceTo(to));
    }

    [Fact]
    public void DistanceTo_VerticalNeighbour_ReturnsOne()
    {
        var from = new Position(0, 0);
        var to   = new Position(1, 0);

        Assert.Equal(1.0, from.DistanceTo(to));
    }

    [Fact]
    public void DistanceTo_DiagonalNeighbour_ReturnsSqrtTwo()
    {
        var from = new Position(0, 0);
        var to   = new Position(1, 1);

        Assert.Equal(Math.Sqrt(2), from.DistanceTo(to), precision: 10);
    }

    [Fact]
    public void DistanceTo_ThreeFourFive_ReturnsFive()
    {
        var from = new Position(0, 0);
        var to   = new Position(3, 4);

        Assert.Equal(5.0, from.DistanceTo(to), precision: 10);
    }

    // ── ManhattanDistanceTo ───────────────────────────────────────────────────

    [Fact]
    public void ManhattanDistanceTo_SamePosition_ReturnsZero()
    {
        var p = new Position(2, 5);

        Assert.Equal(0, p.ManhattanDistanceTo(new Position(2, 5)));
    }

    [Fact]
    public void ManhattanDistanceTo_HorizontalNeighbour_ReturnsOne()
    {
        var from = new Position(0, 0);

        Assert.Equal(1, from.ManhattanDistanceTo(new Position(0, 1)));
    }

    [Fact]
    public void ManhattanDistanceTo_DiagonalNeighbour_ReturnsTwo()
    {
        var from = new Position(0, 0);

        Assert.Equal(2, from.ManhattanDistanceTo(new Position(1, 1)));
    }

    [Fact]
    public void ManhattanDistanceTo_NegativeOffset_UsesAbsoluteValue()
    {
        var from = new Position(3, 3);
        var to   = new Position(0, 0);

        Assert.Equal(6, from.ManhattanDistanceTo(to));
    }

    // ── ToString ──────────────────────────────────────────────────────────────

    [Fact]
    public void ToString_FormatsAsRowCommaCol()
    {
        var p = new Position(2, 7);

        Assert.Equal("(2,7)", p.ToString());
    }

    [Fact]
    public void ToString_ZeroZero_FormatsCorrectly()
    {
        var p = new Position(0, 0);

        Assert.Equal("(0,0)", p.ToString());
    }
}
