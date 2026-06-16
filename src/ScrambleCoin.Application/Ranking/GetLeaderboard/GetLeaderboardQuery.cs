using MediatR;

namespace ScrambleCoin.Application.Ranking.GetLeaderboard;

/// <summary>
/// Returns the full ranking leaderboard, sorted by points descending.
/// </summary>
public sealed record GetLeaderboardQuery : IRequest<IReadOnlyList<LeaderboardEntryDto>>;
