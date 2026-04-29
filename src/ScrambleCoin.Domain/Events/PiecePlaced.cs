using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Events;

/// <summary>
/// Raised when a player places an off-board piece onto the board during PlacePhase.
/// </summary>
public sealed record PiecePlaced(
    Guid GameId,
    int TurnNumber,
    Guid PlayerId,
    Guid PieceId,
    Position Position,
    bool CoinCollected,
    int CoinValue,
    DateTimeOffset OccurredAt) : IDomainEvent;
