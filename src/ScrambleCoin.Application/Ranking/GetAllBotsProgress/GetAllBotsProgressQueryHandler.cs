using MediatR;
using ScrambleCoin.Application.Interfaces;

namespace ScrambleCoin.Application.Ranking.GetAllBotsProgress;

/// <summary>
/// Handles <see cref="GetAllBotsProgressQuery"/>: aggregates all bot villain-defeat records
/// and joins them with ranking-track data to produce per-bot progress summaries.
/// </summary>
public sealed class GetAllBotsProgressQueryHandler
    : IRequestHandler<GetAllBotsProgressQuery, IReadOnlyList<BotProgressDto>>
{
    private readonly IBotUnlocksRepository _botUnlocksRepository;
    private readonly IRankingRepository _rankingRepository;

    public GetAllBotsProgressQueryHandler(
        IBotUnlocksRepository botUnlocksRepository,
        IRankingRepository rankingRepository)
    {
        _botUnlocksRepository = botUnlocksRepository;
        _rankingRepository = rankingRepository;
    }

    public async Task<IReadOnlyList<BotProgressDto>> Handle(
        GetAllBotsProgressQuery request,
        CancellationToken cancellationToken)
    {
        // Load all defeat records and ranking tracks sequentially to avoid a concurrent
        // DbContext operation (both repositories share the same scoped DbContext instance).
        var allUnlocks    = await _botUnlocksRepository.GetAllAsync(cancellationToken);
        var rankingTracks = await _rankingRepository.GetAllAsync(cancellationToken);

        // Build a BotId → BotName lookup from ranking tracks.
        var botNames = rankingTracks.ToDictionary(t => t.BotId, t => t.BotName);

        // Group defeat records by BotId.
        var grouped = allUnlocks
            .GroupBy(u => u.BotId)
            .Select(g =>
            {
                var botId = g.Key;
                var botName = botNames.TryGetValue(botId, out var name)
                    ? name
                    : botId.ToString()[..8] + "…";

                var villainsDefeated = g.Select(u => u.VillainId).Distinct().Count();
                var piecesUnlocked   = g.Count(u => !string.IsNullOrEmpty(u.UnlockedPieceId));

                return new BotProgressDto(botId, botName, villainsDefeated, piecesUnlocked);
            })
            .OrderByDescending(p => p.VillainsDefeated)
            .ThenByDescending(p => p.PiecesUnlocked)
            .ToList()
            .AsReadOnly();

        return grouped;
    }
}
