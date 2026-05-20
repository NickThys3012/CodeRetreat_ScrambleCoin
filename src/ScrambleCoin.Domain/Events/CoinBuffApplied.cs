namespace ScrambleCoin.Domain.Events;

/// <summary>
/// Raised when a coin buff is applied to a piece for their next collection (e.g., Mike Wazowski +1 coin).
/// </summary>
public sealed record CoinBuffApplied(
    Guid GameId,
    int TurnNumber,
    Guid SourcePieceId,
    Guid AffectedPieceId,
    int BuffAmount,
    DateTimeOffset Timestamp)
    : IDomainEvent;
