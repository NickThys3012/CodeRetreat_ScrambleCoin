using MediatR;

namespace ScrambleCoin.Application.Notifications;

/// <summary>
/// Published when a game turn rolls over (both players have completed their moves)
/// and the game has entered the <c>CoinSpawn</c> phase for the next turn.
/// </summary>
/// <param name="GameId">The identifier of the game that advanced to the next turn.</param>
public sealed record TurnRolledOver(Guid GameId) : INotification;
