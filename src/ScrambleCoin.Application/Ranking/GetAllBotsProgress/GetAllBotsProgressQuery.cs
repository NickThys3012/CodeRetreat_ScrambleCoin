using MediatR;

namespace ScrambleCoin.Application.Ranking.GetAllBotsProgress;

/// <summary>
/// Query to retrieve solo-mode villain-path progress for every bot that has played solo mode.
/// Results are ordered by villains defeated descending.
/// </summary>
public sealed record GetAllBotsProgressQuery : IRequest<IReadOnlyList<BotProgressDto>>;
