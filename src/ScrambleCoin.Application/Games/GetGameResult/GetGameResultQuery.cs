using MediatR;

namespace ScrambleCoin.Application.Games.GetGameResult;

/// <summary>
/// Query to retrieve the final result of a finished game.
/// </summary>
/// <param name="GameId">The game to retrieve the result for.</param>
/// <param name="BotToken">The bearer token of the requesting bot — used to verify the bot participated in this game.</param>
public sealed record GetGameResultQuery(Guid GameId, Guid BotToken) : IRequest<GetGameResultDto>;
