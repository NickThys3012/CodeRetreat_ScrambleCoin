using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Events;

/// <summary>
/// Raised when a player's piece completes all its move actions during MovePhase.
/// </summary>
/// <param name="GameId">The identifier of the game.</param>
/// <param name="TurnNumber">The turn on which the piece moved.</param>
/// <param name="PlayerId">The player who owns the piece.</param>
/// <param name="PieceId">The identifier of the piece that moved.</param>
/// <param name="From">The position the piece started at.</param>
/// <param name="To">The final position of the piece after all moves.</param>
/// <param name="Path">Every tile visited during the move (in order), including the final position.</param>
/// <param name="OccurredAt">UTC timestamp when the event was raised.</param>
public sealed record PieceMoved(
    Guid GameId,
    int TurnNumber,
    Guid PlayerId,
    Guid PieceId,
    Position From,
    Position To,
    IReadOnlyList<Position> Path,
    DateTimeOffset OccurredAt) : IDomainEvent;
