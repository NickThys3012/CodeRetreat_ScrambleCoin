using ScrambleCoin.Domain.Enums;

namespace ScrambleCoin.Domain.Events;

/// <summary>
/// Raised each time the active phase within a turn changes.
/// </summary>
/// <param name="GameId">The identifier of the game in which the phase advanced.</param>
/// <param name="TurnNumber">The turn number (1–5) during which the phase advanced.</param>
/// <param name="PreviousPhase">The phase that just ended.</param>
/// <param name="NewPhase">
/// The phase that is now active, or <c>null</c> when the final <see cref="TurnPhase.MovePhase"/>
/// of turn 5 has completed and the game is ending.
/// </param>
/// <param name="OccurredAt">UTC timestamp when the event was raised.</param>
public sealed record TurnPhaseAdvanced(
    Guid GameId,
    int TurnNumber,
    TurnPhase PreviousPhase,
    TurnPhase? NewPhase,
    DateTimeOffset OccurredAt) : IDomainEvent;
