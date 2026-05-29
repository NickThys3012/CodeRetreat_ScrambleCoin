using MediatR;

namespace ScrambleCoin.Application.Games.GetBoardState;

/// <summary>
/// Returns the two player IDs for a game together with its current phase and active mover.
/// Used internally (e.g. by <c>GameBroadcaster</c>) to decide which player groups to
/// send <c>ActionRequired</c> notifications to without fetching the full board state.
/// </summary>
public sealed record GetGamePlayerIdsQuery(Guid GameId) : IRequest<GamePlayerIdsDto>;

/// <summary>Minimal game-state projection used for player-targeted SignalR notifications.</summary>
/// <param name="PlayerOne">The first player's ID.</param>
/// <param name="PlayerTwo">The second player's ID.</param>
/// <param name="Phase">Current phase name ("CoinSpawn", "PlacePhase", "MovePhase"), or null.</param>
/// <param name="ActiveMover">The player whose turn it is in MovePhase, or null outside MovePhase.</param>
public sealed record GamePlayerIdsDto(
    Guid PlayerOne,
    Guid PlayerTwo,
    string? Phase,
    Guid? ActiveMover);
