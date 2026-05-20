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
    AnyDirection
}
