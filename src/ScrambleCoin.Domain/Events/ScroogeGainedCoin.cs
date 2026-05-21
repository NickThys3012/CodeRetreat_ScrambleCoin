namespace ScrambleCoin.Domain.Events;

/// <summary>
/// Raised when Scrooge's passive ability triggers at the end of the MovePhase.
/// </summary>
public sealed record ScroogeGainedCoin(
    Guid GameId,
    int TurnNumber,
    Guid PieceId,
    int CoinsGained,
    DateTimeOffset Timestamp)
    : IDomainEvent;
