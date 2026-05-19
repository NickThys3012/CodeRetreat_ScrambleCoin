using MediatR;

namespace ScrambleCoin.Application.Games.GetBoardState;

/// <summary>
/// Query to retrieve the current board state for a given game, authenticated by bot token.
/// </summary>
/// <param name="GameId">The game to query.</param>
/// <param name="BotToken">Bearer token from the X-Bot-Token header.</param>
public sealed record GetBoardStateQuery(
    Guid GameId,
    Guid BotToken) : IRequest<BoardStateDto>;
