namespace ScrambleCoin.Domain.Events;

/// <summary>
/// Raised when a game transitions from <c>WaitingForBots</c> to <c>InProgress</c>.
/// </summary>
/// <param name="GameId">The identifier of the game that started.</param>
/// <param name="PlayerOne">The identifier of player one.</param>
/// <param name="PlayerTwo">The identifier of player two.</param>
/// <param name="OccurredAt">UTC timestamp when the event was raised.</param>
public sealed record GameStarted(
    Guid GameId,
    Guid PlayerOne,
    Guid PlayerTwo,
    DateTimeOffset OccurredAt) : IDomainEvent;
