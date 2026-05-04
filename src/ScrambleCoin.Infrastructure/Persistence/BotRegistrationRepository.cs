using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Domain.BotRegistrations;
using ScrambleCoin.Infrastructure.Persistence.Records;

namespace ScrambleCoin.Infrastructure.Persistence;

/// <summary>
/// EF Core-backed implementation of <see cref="IBotRegistrationRepository"/>.
/// </summary>
public sealed class BotRegistrationRepository : IBotRegistrationRepository
{
    private readonly ScrambleCoinDbContext _context;

    public BotRegistrationRepository(ScrambleCoinDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<BotRegistration?> GetByTokenAsync(Guid token, CancellationToken cancellationToken = default)
    {
        var record = await _context.BotRegistrations.FindAsync([token], cancellationToken);
        if (record is null) return null;

        return new BotRegistration(record.Token, record.PlayerId, record.GameId);
    }

    /// <inheritdoc/>
    public async Task SaveAsync(BotRegistration botRegistration, CancellationToken cancellationToken = default)
    {
        var record = new BotRegistrationRecord
        {
            Token = botRegistration.Token,
            PlayerId = botRegistration.PlayerId,
            GameId = botRegistration.GameId
        };

        var existing = await _context.BotRegistrations.FindAsync([botRegistration.Token], cancellationToken);
        if (existing is null)
        {
            _context.BotRegistrations.Add(record);
        }
        else
        {
            _context.Entry(existing).CurrentValues.SetValues(record);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
