namespace ScrambleCoin.Domain.Events;

/// <summary>
/// Raised when a piece steals a coin from another player.
/// This happens when Daisy lands on an opponent piece and swaps positions.
/// </summary>
/// <param name="GameId">The identifier of the game.</param>
/// <param name="TurnNumber">The turn on which the coin was stolen.</param>
/// <param name="FromPlayerId">The player ID of the opponent losing the coin.</param>
/// <param name="ToPlayerId">The player ID of the player stealing the coin (typically owns Daisy).</param>
/// <param name="StealingPieceId">The identifier of the piece performing the steal (e.g., Daisy).</param>
/// <param name="CoinValue">The value of the coin stolen.</param>
/// <param name="OccurredAt">UTC timestamp when the event was raised.</param>
public sealed record CoinStolen(
    Guid GameId,
    int TurnNumber,
    Guid FromPlayerId,
    Guid ToPlayerId,
    Guid StealingPieceId,
    int CoinValue,
    DateTimeOffset OccurredAt) : IDomainEvent;
