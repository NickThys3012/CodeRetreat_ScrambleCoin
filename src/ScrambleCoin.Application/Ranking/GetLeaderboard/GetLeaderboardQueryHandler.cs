using MediatR;
using ScrambleCoin.Application.Interfaces;

namespace ScrambleCoin.Application.Ranking.GetLeaderboard;

/// <summary>
/// Handles <see cref="GetLeaderboardQuery"/>:
/// loads all ranking tracks, sorts them, assigns ranks, and returns the DTO list.
/// </summary>
/// <remarks>
/// Sort order: points descending, then wins descending (tie-break), then games played ascending (fewest games needed to reach the score).
/// </remarks>
public sealed class GetLeaderboardQueryHandler
    : IRequestHandler<GetLeaderboardQuery, IReadOnlyList<LeaderboardEntryDto>>
{
    private readonly IRankingRepository _rankingRepository;

    public GetLeaderboardQueryHandler(IRankingRepository rankingRepository)
    {
        _rankingRepository = rankingRepository;
    }

    public async Task<IReadOnlyList<LeaderboardEntryDto>> Handle(
        GetLeaderboardQuery request,
        CancellationToken cancellationToken)
    {
        var tracks = await _rankingRepository.GetAllAsync(cancellationToken);

        var sorted = tracks
            .OrderByDescending(t => t.Points)
            .ThenByDescending(t => t.Wins)
            .ThenBy(t => t.GamesPlayed)
            .ToList();

        var result = sorted
            .Select((track, index) => new LeaderboardEntryDto(
                BotId:       track.BotId,
                BotName:     track.BotName,
                Points:      track.Points,
                Wins:        track.Wins,
                Draws:       track.Draws,
                Losses:      track.Losses,
                GamesPlayed: track.GamesPlayed,
                Rank:        index + 1))
            .ToList()
            .AsReadOnly();

        return result;
    }
}
