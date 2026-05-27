namespace ScrambleCoin.Domain.Events;

/// <summary>
/// Raised when a move buff is applied to one or more pieces (e.g. Fairy Godmother +1 move to allies).
/// </summary>
public sealed record MoveBuffApplied(
    Guid GameId,
    int TurnNumber,
    Guid SourcePieceId,
    IReadOnlyList<Guid> AffectedPieceIds,
    int BuffAmount,
    DateTimeOffset Timestamp)
    : IDomainEvent;
