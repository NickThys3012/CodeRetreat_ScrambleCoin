using ScrambleCoin.Domain.Entities;

namespace ScrambleCoin.Application.Interfaces;

/// <summary>
/// Provides persistence operations for <see cref="Game"/> aggregates.
/// </summary>
public interface IGameRepository
{
    /// <summary>Retrieves a game by its unique identifier.</summary>
    public Task<Game> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Persists the current state of a game.</summary>
    public Task SaveAsync(Game game, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages the game for persistence without committing to the store.
    /// The caller is responsible for calling <see cref="IUnitOfWork.SaveChangesAsync"/>
    /// to flush all staged changes in a single transaction.
    /// </summary>
    public Task StageAsync(Game game, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <c>true</c> when the given player has an active (InProgress) game.
    /// </summary>
    /// <param name="playerId">The player-slot identifier to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> HasActiveGameAsync(Guid playerId, CancellationToken cancellationToken = default);
}
