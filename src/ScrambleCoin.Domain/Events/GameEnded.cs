namespace ScrambleCoin.Domain.Events;

/// <summary>
/// Raised when a game transitions to <c>Finished</c> after the 5th turn.
/// </summary>
/// <param name="GameId">The identifier of the game that ended.</param>
/// <param name="PlayerOneScore">Final score of player one.</param>
/// <param name="PlayerTwoScore">Final score of player two.</param>
/// <param name="WinnerId">
/// The identifier of the winning player, or <c>null</c> when the game ended in a draw.
/// </param>
/// <param name="IsDraw"><c>true</c> when both players share the same final score.</param>
/// <param name="OccurredAt">UTC timestamp when the event was raised.</param>
public sealed record GameEnded(
    Guid GameId,
    int PlayerOneScore,
    int PlayerTwoScore,
    Guid? WinnerId,
    bool IsDraw,
    DateTimeOffset OccurredAt) : IDomainEvent;
