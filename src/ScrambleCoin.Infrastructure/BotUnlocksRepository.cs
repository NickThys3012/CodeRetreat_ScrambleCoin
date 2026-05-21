using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Infrastructure.Persistence;

namespace ScrambleCoin.Infrastructure;

/// <summary>
/// EF Core-backed implementation of <see cref="IBotUnlocksRepository"/>.
/// </summary>
public sealed class BotUnlocksRepository : IBotUnlocksRepository
{
    private readonly ScrambleCoinDbContext _context;

    public BotUnlocksRepository(ScrambleCoinDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<BotUnlock>> GetDefeatedVillainsAsync(Guid botId, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(
            _context.BotUnlocks.Where(bu => bu.BotId == botId).ToList());
    }

    public async Task<IEnumerable<string>> GetUnlockedPieceIdsAsync(Guid botId, CancellationToken cancellationToken = default)
    {
        var pieceIds = await Task.FromResult(
            _context.BotUnlocks
                .Where(bu => bu.BotId == botId && bu.UnlockedPieceId != null)
                .Select(bu => bu.UnlockedPieceId!)
                .ToList());
        return pieceIds;
    }

    public async Task RecordDefeatAsync(Guid botId, string villainId, string? unlockedPieceId, CancellationToken cancellationToken = default)
    {
        // UPSERT: update if exists, insert if not
        var existing = _context.BotUnlocks.FirstOrDefault(bu => bu.BotId == botId && bu.VillainId == villainId);

        if (existing != null)
        {
            // Update: bot is re-challenging this villain
            existing.DefeatedAtUtc = DateTime.UtcNow;
            if (unlockedPieceId != null && string.IsNullOrEmpty(existing.UnlockedPieceId))
            {
                existing.UnlockedPieceId = unlockedPieceId;
            }
            _context.BotUnlocks.Update(existing);
        }
        else
        {
            // Insert: first time defeating this villain
            var unlock = new BotUnlock
            {
                Id = Guid.NewGuid(),
                BotId = botId,
                VillainId = villainId,
                UnlockedPieceId = unlockedPieceId,
                DefeatedAtUtc = DateTime.UtcNow
            };
            _context.BotUnlocks.Add(unlock);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> HasDefeatedVillainAsync(Guid botId, string villainId, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(
            _context.BotUnlocks.Any(bu => bu.BotId == botId && bu.VillainId == villainId));
    }
}
