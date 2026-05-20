using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Events;

/// <summary>
/// Raised when a coin is converted from silver to gold (e.g., by Merlin's ability).
/// </summary>
public sealed record CoinConverted(
    Guid GameId,
    int TurnNumber,
    Position Position,
    string FromType,
    string ToType,
    DateTimeOffset Timestamp)
    : IDomainEvent;
