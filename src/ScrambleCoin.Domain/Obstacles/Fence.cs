using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Obstacles;

/// <summary>
/// A fence placed on the edge between two orthogonally adjacent tiles.
/// Blocks movement across that edge for orthogonal moves.
/// Two fences meeting in a corner also block diagonal movement through that corner.
/// </summary>
public sealed class Fence
{
    /// <summary>One side of the fenced edge.</summary>
    public Position From { get; }

    /// <summary>The other side of the fenced edge.</summary>
    public Position To { get; }

    public Fence(Position from, Position to)
    {
        if (!from.IsOrthogonallyAdjacentTo(to))
            throw new DomainException(
                $"Fence positions {from} and {to} are not orthogonally adjacent.");

        From = from;
        To = to;
    }

    /// <summary>
    /// Returns true if this fence is on the edge between <paramref name="a"/> and <paramref name="b"/>
    /// (regardless of direction).
    /// </summary>
    public bool IsOnEdge(Position a, Position b) =>
        (From == a && To == b) || (From == b && To == a);
}
