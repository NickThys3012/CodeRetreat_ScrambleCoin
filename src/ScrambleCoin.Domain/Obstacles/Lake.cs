using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Obstacles;

/// <summary>
/// A 2×2 impassable obstacle. The top-left position defines the lake;
/// it covers (Row, Col), (Row, Col+1), (Row+1, Col), and (Row+1, Col+1).
/// </summary>
public sealed class Lake
{
    /// <summary>The top-left position of the lake.</summary>
    public Position TopLeft { get; }

    /// <summary>All four positions covered by this lake.</summary>
    public IReadOnlyList<Position> CoveredPositions { get; }

    public Lake(Position topLeft)
    {
        // Validate that the 2×2 area fits within the 8×8 grid.
        if (topLeft.Row + 1 > Position.MaxValue)
            throw new DomainException(
                $"Lake at {topLeft} would extend beyond the bottom edge of the board.");

        if (topLeft.Col + 1 > Position.MaxValue)
            throw new DomainException(
                $"Lake at {topLeft} would extend beyond the right edge of the board.");

        TopLeft = topLeft;
        CoveredPositions =
        [
            new Position(topLeft.Row,     topLeft.Col),
            new Position(topLeft.Row,     topLeft.Col + 1),
            new Position(topLeft.Row + 1, topLeft.Col),
            new Position(topLeft.Row + 1, topLeft.Col + 1),
        ];
    }

    /// <summary>Returns true if this lake covers the given position.</summary>
    public bool Covers(Position position) =>
        CoveredPositions.Any(p => p == position);
}
