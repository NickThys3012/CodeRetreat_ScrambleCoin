using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Events;

/// <summary>
/// Raised when a Fence obstacle is destroyed during gameplay.
/// This happens when Ralph, Pumbaa, or Stitch encounters/destroys a fence.
/// </summary>
/// <param name="GameId">The identifier of the game.</param>
/// <param name="TurnNumber">The turn on which the fence was destroyed.</param>
/// <param name="Position">One of the two tile positions that the destroyed fence connected.</param>
/// <param name="OccurredAt">UTC timestamp when the event was raised.</param>
public sealed record FenceDestroyed(
    Guid GameId,
    int TurnNumber,
    Position Position,
    DateTimeOffset OccurredAt) : IDomainEvent;
