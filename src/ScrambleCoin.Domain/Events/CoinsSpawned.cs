using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Events;

/// <summary>
/// Raised when coins are spawned onto the board during the <c>CoinSpawn</c> phase.
/// </summary>
/// <param name="GameId">The identifier of the game.</param>
/// <param name="TurnNumber">The turn on which the coins were spawned.</param>
/// <param name="Coins">The positions and types of all coins spawned.</param>
/// <param name="OccurredAt">UTC timestamp when the event was raised.</param>
public sealed record CoinsSpawned(
    Guid GameId,
    int TurnNumber,
    IReadOnlyList<(Position Position, CoinType CoinType)> Coins,
    DateTimeOffset OccurredAt) : IDomainEvent;
