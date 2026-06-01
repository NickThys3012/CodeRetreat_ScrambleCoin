using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using AppBoardStateDto = ScrambleCoin.Application.Games.GetBoardState.BoardStateDto;
using ScrambleCoin.Application.Games.Replay;
using ScrambleCoin.Infrastructure.Persistence.Records;

namespace ScrambleCoin.Infrastructure.Persistence;

public sealed class GameSnapshotRepository : IGameSnapshotRepository
{
    private readonly ScrambleCoinDbContext _context;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public GameSnapshotRepository(ScrambleCoinDbContext context)
    {
        _context = context;
    }

    public async Task SaveSnapshotAsync(Guid gameId, int turn, string? phase, string boardStateJson, CancellationToken ct = default)
    {
        // Use a raw SQL MAX to avoid loading all rows just to get the next sequence number.
        var maxSeq = await _context.GameSnapshots
            .Where(s => s.GameId == gameId)
            .Select(s => (int?)s.SequenceNumber)
            .MaxAsync(ct) ?? 0;

        _context.GameSnapshots.Add(new GameSnapshotRecord
        {
            GameId         = gameId,
            SequenceNumber = maxSeq + 1,
            Turn           = turn,
            Phase          = phase,
            BoardStateJson = boardStateJson,
            CapturedAt     = DateTimeOffset.UtcNow
        });

        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ReplayFrameDto>> GetFramesAsync(Guid gameId, CancellationToken ct = default)
    {
        var records = await _context.GameSnapshots
            .Where(s => s.GameId == gameId)
            .OrderBy(s => s.SequenceNumber)
            .AsNoTracking()
            .ToListAsync(ct);

        return records
            .Select(r =>
            {
                var boardState = JsonSerializer.Deserialize<AppBoardStateDto>(r.BoardStateJson, _json)
                    ?? throw new InvalidOperationException($"Failed to deserialise snapshot {r.Id}");
                return new ReplayFrameDto(r.SequenceNumber, r.Turn, r.Phase, r.CapturedAt, boardState);
            })
            .ToList()
            .AsReadOnly();
    }
}
