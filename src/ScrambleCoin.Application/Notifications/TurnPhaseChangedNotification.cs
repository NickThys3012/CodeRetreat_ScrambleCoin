using MediatR;

namespace ScrambleCoin.Application.Notifications;

/// <summary>
/// Published whenever a turn phase transition occurs in a game
/// (e.g. CoinSpawn → PlacePhase, PlacePhase → MovePhase, or MovePhase → CoinSpawn when the turn rolls over).
/// Consumed by the Web layer to broadcast a <c>PhaseChanged</c> SignalR event to spectators.
/// </summary>
/// <param name="GameId">The game in which the phase changed.</param>
/// <param name="TurnNumber">The turn during which the transition occurred.</param>
/// <param name="PreviousPhase">String name of the phase that just ended (e.g. "PlacePhase").</param>
/// <param name="NewPhase">
/// String name of the phase that is now active, or <c>null</c> when the game is ending
/// after the final MovePhase.
/// </param>
public sealed record TurnPhaseChangedNotification(
    Guid GameId,
    int TurnNumber,
    string? PreviousPhase,
    string? NewPhase) : INotification;
