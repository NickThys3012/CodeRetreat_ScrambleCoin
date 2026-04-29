using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Events;

/// <summary>
/// Raised when a player removes an on-board piece and places a different lineup piece
/// in its stead during PlacePhase.
/// </summary>
public sealed record PieceReplaced(
    Guid GameId,
    int TurnNumber,
    Guid PlayerId,
    Guid RemovedPieceId,
    Guid PlacedPieceId,
    Position Position,
    bool CoinCollected,
    int CoinValue,
    DateTimeOffset OccurredAt) : IDomainEvent;
