using MediatR;

namespace ScrambleCoin.Application.Games.GetBoardState;

/// <summary>
/// Query to retrieve the current board state for spectators, without requiring bot authentication.
/// Returns the board state from PlayerOne's perspective (YourScore = PlayerOne, OpponentScore = PlayerTwo).
/// Intended for internal use by the SignalR broadcaster only.
/// </summary>
/// <param name="GameId">The game to query.</param>
public sealed record GetSpectatorBoardStateQuery(Guid GameId) : IRequest<BoardStateDto>;
