using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Events;

/// <summary>
/// Raised when a piece is removed from the board during gameplay.
/// This happens when Scar lands on an opponent piece and removes it.
/// </summary>
/// <param name="GameId">The identifier of the game.</param>
/// <param name="TurnNumber">The turn on which the piece was removed.</param>
/// <param name="RemovedPieceId">The identifier of the piece that was removed.</param>
/// <param name="RemovedByPieceId">The identifier of the piece that removed it (e.g., Scar).</param>
/// <param name="Position">The position where the piece was removed from.</param>
/// <param name="OccurredAt">UTC timestamp when the event was raised.</param>
public sealed record PieceRemoved(
    Guid GameId,
    int TurnNumber,
    Guid RemovedPieceId,
    Guid RemovedByPieceId,
    Position Position,
    DateTimeOffset OccurredAt) : IDomainEvent;
