using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;

namespace ScrambleCoin.Infrastructure.Persistence;

/// <summary>
/// EF Core-backed implementation of <see cref="IGameRepository"/>.
/// Full persistence will be added when the Game entity is mapped to the database.
/// </summary>
public sealed class GameRepository : IGameRepository
{
    private readonly ScrambleCoinDbContext _context;

    public GameRepository(ScrambleCoinDbContext context)
    {
        _context = context;
    }

    public Task<Game> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Game persistence is not yet configured. Implement in a future issue.");

    public Task SaveAsync(Game game, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Game persistence is not yet configured. Implement in a future issue.");
}
