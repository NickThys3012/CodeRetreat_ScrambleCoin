namespace ScrambleCoin.Domain.Enums;

/// <summary>
/// Determines the directions in which a piece may move.
/// </summary>
public enum MovementType
{
    /// <summary>The piece may move up, down, left, or right (cardinal directions).</summary>
    Orthogonal,

    /// <summary>The piece may move along the four diagonal directions.</summary>
    Diagonal,

    /// <summary>The piece may move in any direction (orthogonal or diagonal).</summary>
    AnyDirection,

    /// <summary>
    /// The piece teleports directly to its destination, ignoring all obstacles and pieces along the way.
    /// Only collects coins at the destination tile, not intermediate tiles.
    /// </summary>
    Jump,

    /// <summary>The piece slides in a chosen direction until hitting an obstacle, piece, or board edge. The player does not choose how far it goes.</summary>
    Charge,

    /// <summary>
    /// The piece passes through pieces and obstacles on intermediate tiles but must end on a free tile.
    /// Collects coins on all tiles in the path (intermediate and destination).
    /// Fences still block Ethereal movement.
    /// </summary>
    Ethereal
}
