using ScrambleCoin.Domain.Entities;

namespace ScrambleCoin.Application.Interfaces;

/// <summary>
/// Persistence operations for <see cref="RankingTrack"/> aggregates.
/// </summary>
public interface IRankingRepository
{
    /// <summary>Retrieves the ranking track for the given bot, or <c>null</c> if none exists yet.</summary>
    Task<RankingTrack?> GetByBotIdAsync(Guid botId, CancellationToken ct = default);

    /// <summary>Returns all ranking tracks, unordered.</summary>
    Task<IReadOnlyList<RankingTrack>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Inserts or updates the given ranking track.</summary>
    Task SaveAsync(RankingTrack track, CancellationToken ct = default);
}
