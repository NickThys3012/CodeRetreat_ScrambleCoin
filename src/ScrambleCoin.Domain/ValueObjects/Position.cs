using ScrambleCoin.Domain.Exceptions;

namespace ScrambleCoin.Domain.ValueObjects;

/// <summary>
/// Represents a position on the 8×8 board. Row and Col are both in [0, 7].
/// </summary>
public sealed class Position : IEquatable<Position>
{
    public const int MinValue = 0;
    public const int MaxValue = 7;

    public int Row { get; }
    public int Col { get; }

    public Position(int row, int col)
    {
        if (row is < MinValue or > MaxValue)
            throw new DomainException($"Row {row} is out of bounds. Must be between {MinValue} and {MaxValue}.");

        if (col is < MinValue or > MaxValue)
            throw new DomainException($"Col {col} is out of bounds. Must be between {MinValue} and {MaxValue}.");

        Row = row;
        Col = col;
    }

    /// <summary>Returns true if this position is orthogonally adjacent to <paramref name="other"/>.</summary>
    public bool IsOrthogonallyAdjacentTo(Position other)
    {
        var rowDiff = Math.Abs(Row - other.Row);
        var colDiff = Math.Abs(Col - other.Col);
        return (rowDiff == 1 && colDiff == 0) || (rowDiff == 0 && colDiff == 1);
    }

    /// <summary>Returns true if this position is diagonally adjacent to <paramref name="other"/>.</summary>
    public bool IsDiagonallyAdjacentTo(Position other)
    {
        var rowDiff = Math.Abs(Row - other.Row);
        var colDiff = Math.Abs(Col - other.Col);
        return rowDiff == 1 && colDiff == 1;
    }

    public bool Equals(Position? other)
    {
        if (other is null) return false;
        return Row == other.Row && Col == other.Col;
    }

    public override bool Equals(object? obj) => obj is Position p && Equals(p);

    public override int GetHashCode() => HashCode.Combine(Row, Col);

    public static bool operator ==(Position? left, Position? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Position? left, Position? right) => !(left == right);

    public override string ToString() => $"({Row}, {Col})";
}
