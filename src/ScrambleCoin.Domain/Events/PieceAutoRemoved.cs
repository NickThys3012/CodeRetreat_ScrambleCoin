namespace ScrambleCoin.Domain.Events;

/// <summary>
/// Raised when a piece is automatically removed from the board (e.g., Cinderella at turn 5 start, or Forky after first move).
/// </summary>
public sealed record PieceAutoRemoved(
    Guid GameId,
    int TurnNumber,
    Guid PieceId,
    string Reason,
    DateTimeOffset Timestamp)
    : IDomainEvent;
