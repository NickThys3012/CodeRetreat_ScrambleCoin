using MediatR;

namespace ScrambleCoin.Application.Games.Matchmaking;

/// <summary>
/// Command dispatched by the QueueService when two bots are matched.
/// Creates a game shell, assigns both bot lineups, starts the game,
/// and persists everything in one atomic operation.
/// </summary>
public sealed record StartMatchCommand(
    IReadOnlyList<string> LineupOne,
    Guid? BotTokenOne,
    IReadOnlyList<string> LineupTwo,
    Guid? BotTokenTwo) : IRequest<StartMatchResult>;
