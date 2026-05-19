using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;

namespace ScrambleCoin.Domain.ValueObjects;

/// <summary>
/// Represents movement constraints for a single segment of a multi-step movement sequence.
/// </summary>
public sealed class MovementPattern
{
    public MovementType MovementType { get; }
    public int MaxDistance { get; }

    public MovementPattern(MovementType movementType, int maxDistance)
    {
        if (maxDistance < 1)
            throw new DomainException($"MaxDistance must be at least 1, but was {maxDistance}.");
        
        MovementType = movementType;
        MaxDistance = maxDistance;
    }

    public override bool Equals(object? obj) =>
        obj is MovementPattern other && MovementType == other.MovementType && MaxDistance == other.MaxDistance;

    public override int GetHashCode() => HashCode.Combine(MovementType, MaxDistance);
}
