using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Events;

/// <summary>
/// Raised when a piece steps onto (or starts on) a tile containing a coin during MovePhase.
/// </summary>
/// <param name="GameId">The identifier of the game.</param>
/// <param name="TurnNumber">The turn on which the coin was collected.</param>
/// <param name="PlayerId">The player who collected the coin.</param>
/// <param name="PieceId">The identifier of the piece that collected the coin.</param>
/// <param name="Position">The board position where the coin was collected.</param>
/// <param name="CoinType">The type of coin that was collected.</param>
/// <param name="Value">The point value of the collected coin.</param>
/// <param name="OccurredAt">UTC timestamp when the event was raised.</param>
public sealed record CoinCollected(
    Guid GameId,
    int TurnNumber,
    Guid PlayerId,
    Guid PieceId,
    Position Position,
    CoinType CoinType,
    int Value,
    DateTimeOffset OccurredAt) : IDomainEvent;
