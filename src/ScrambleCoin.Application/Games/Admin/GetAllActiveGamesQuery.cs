using MediatR;

namespace ScrambleCoin.Application.Games.Admin;

/// <summary>
/// Query to retrieve summary information for all active games
/// (status <c>InProgress</c> or <c>WaitingForBots</c>).
/// Used by the admin dashboard to monitor live games.
/// </summary>
public sealed record GetAllActiveGamesQuery : IRequest<IReadOnlyList<ActiveGameSummaryDto>>;
