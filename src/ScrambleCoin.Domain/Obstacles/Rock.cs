using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Obstacles;

/// <summary>
/// A 1-tile impassable obstacle on the board.
/// </summary>
public sealed class Rock
{
    public Position Position { get; }

    public Rock(Position position)
    {
        Position = position;
    }
}
