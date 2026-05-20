using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Events;

/// <summary>
/// Raised when a Rock obstacle is destroyed during gameplay.
/// This happens when Ralph or another piece with rock-destroying ability stops near a rock.
/// </summary>
/// <param name="GameId">The identifier of the game.</param>
/// <param name="TurnNumber">The turn on which the rock was destroyed.</param>
/// <param name="Position">The position where the rock was destroyed.</param>
/// <param name="OccurredAt">UTC timestamp when the event was raised.</param>
public sealed record RockDestroyed(
    Guid GameId,
    int TurnNumber,
    Position Position,
    DateTimeOffset OccurredAt) : IDomainEvent;
