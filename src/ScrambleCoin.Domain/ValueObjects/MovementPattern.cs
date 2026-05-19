using ScrambleCoin.Domain.Enums;

namespace ScrambleCoin.Domain.ValueObjects;

/// <summary>
/// Represents movement constraints for a single segment of a multi-step movement sequence.
/// </summary>
public record MovementPattern(MovementType MovementType, int MaxDistance)
{
    public MovementPattern(MovementType movementType, int maxDistance) : this(movementType, maxDistance)
    {
        if (maxDistance < 1)
            throw new ArgumentException("MaxDistance must be at least 1", nameof(maxDistance));
    }
}
