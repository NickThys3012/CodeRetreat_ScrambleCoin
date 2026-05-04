using MediatR;

namespace ScrambleCoin.Application.Games.CreateGame;

/// <summary>
/// Admin command to create a new game shell.
/// The game is created in <c>WaitingForBots</c> state with two pre-assigned player slot IDs;
/// bots join later via <c>POST /api/games/{gameId}/join</c>.
/// </summary>
public sealed record CreateGameCommand : IRequest<CreateGameResult>;
