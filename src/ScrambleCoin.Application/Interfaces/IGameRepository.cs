using ScrambleCoin.Domain.Entities;

namespace ScrambleCoin.Application.Interfaces;

/// <summary>
/// Provides persistence operations for <see cref="Game"/> aggregates.
/// </summary>
public interface IGameRepository
{
    /// <summary>Retrieves a game by its unique identifier.</summary>
    Task<Game> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Persists the current state of a game.</summary>
    Task SaveAsync(Game game, CancellationToken cancellationToken = default);
}
