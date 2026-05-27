using Microsoft.EntityFrameworkCore;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;
namespace ScrambleCoin.Infrastructure.Persistence;

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
        return await
            _context.BotUnlocks.Where(bu => bu.BotId == botId).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<string>> GetUnlockedPieceIdsAsync(Guid botId, CancellationToken cancellationToken = default)
    {
       return await _context.BotUnlocks
            .Where(bu => bu.BotId == botId && bu.UnlockedPieceId != null)
            .Select(bu => bu.UnlockedPieceId!)
            .ToListAsync(cancellationToken);
    }

    public async Task RecordDefeatAsync(Guid botId, string villainId, string? unlockedPieceId, CancellationToken cancellationToken = default)
    {
        // UPSERT: update if exists, insert if not
        var existing =await _context.BotUnlocks.FirstOrDefaultAsync(bu => bu.BotId == botId && bu.VillainId == villainId,cancellationToken);

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

    public Task<bool> HasDefeatedVillainAsync(Guid botId, string villainId, CancellationToken cancellationToken = default)
    {
        return 
            _context.BotUnlocks.AnyAsync(bu => bu.BotId == botId && bu.VillainId == villainId,cancellationToken);
    }
}
