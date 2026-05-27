namespace ScrambleCoin.Domain.Events;

/// <summary>
/// Raised when a move debuff is applied to one or more pieces (e.g. Ursula −1 move to opponents).
/// </summary>
public sealed record MoveDebuffApplied(
    Guid GameId,
    int TurnNumber,
    Guid SourcePieceId,
    IReadOnlyList<Guid> AffectedPieceIds,
    int DebuffAmount,
    DateTimeOffset Timestamp)
    : IDomainEvent;
