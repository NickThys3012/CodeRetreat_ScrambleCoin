using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Infrastructure.Persistence.Records;

namespace ScrambleCoin.Infrastructure.Persistence;

/// <summary>
/// EF Core-backed implementation of <see cref="IRankingRepository"/>.
/// </summary>
public sealed class RankingRepository : IRankingRepository
{
    private readonly ScrambleCoinDbContext _db;

    public RankingRepository(ScrambleCoinDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<RankingTrack?> GetByBotIdAsync(Guid botId, CancellationToken ct = default)
    {
        var record = await _db.RankingTracks.FindAsync([botId], ct);
        return record is null ? null : MapToDomain(record);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RankingTrack>> GetAllAsync(CancellationToken ct = default)
    {
        var records = await _db.RankingTracks.AsNoTracking().ToListAsync(ct);
        return records.Select(MapToDomain).ToList().AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task SaveAsync(RankingTrack track, CancellationToken ct = default)
    {
        var record = MapToRecord(track);

        var existing = await _db.RankingTracks.FindAsync([track.BotId], ct);
        if (existing is null)
        {
            _db.RankingTracks.Add(record);
        }
        else
        {
            _db.Entry(existing).CurrentValues.SetValues(record);
        }

        await _db.SaveChangesAsync(ct);
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static RankingTrack MapToDomain(RankingTrackRecord record)
    {
        var milestones = string.IsNullOrWhiteSpace(record.MilestonesHitJson)
            ? []
            : JsonSerializer.Deserialize<List<int>>(record.MilestonesHitJson) ?? [];

        return new RankingTrack(
            botId:        record.BotId,
            botName:      record.BotName,
            points:       record.Points,
            wins:         record.Wins,
            draws:        record.Draws,
            losses:       record.Losses,
            gamesPlayed:  record.GamesPlayed,
            milestonesHit: milestones);
    }

    private static RankingTrackRecord MapToRecord(RankingTrack track) => new()
    {
        BotId            = track.BotId,
        BotName          = track.BotName,
        Points           = track.Points,
        Wins             = track.Wins,
        Draws            = track.Draws,
        Losses           = track.Losses,
        GamesPlayed      = track.GamesPlayed,
        MilestonesHitJson = JsonSerializer.Serialize(track.MilestonesHit)
    };
}
