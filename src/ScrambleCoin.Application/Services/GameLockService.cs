using System.Collections.Concurrent;

namespace ScrambleCoin.Application.Services;

/// <summary>
/// Singleton service that provides per-game <see cref="SemaphoreSlim"/> locks.
/// Used by <c>GameSerializationBehaviour</c> to prevent concurrent mutations of the same game.
/// </summary>
public sealed class GameLockService
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    /// <summary>Returns the semaphore for the given game, creating it on first access.</summary>
    public SemaphoreSlim GetLock(Guid gameId)
        => _locks.GetOrAdd(gameId, _ => new SemaphoreSlim(1, 1));
}
