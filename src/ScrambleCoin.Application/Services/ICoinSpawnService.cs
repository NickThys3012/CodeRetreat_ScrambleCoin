using ScrambleCoin.Domain.Entities;

namespace ScrambleCoin.Application.Services;

/// <summary>
/// Spawns coins for the current turn, advances the phase to <c>PlacePhase</c>, and persists.
/// </summary>
public interface ICoinSpawnService
{
    /// <summary>
    /// Loads the game by <paramref name="gameId"/>, spawns coins, advances phase, and saves.
    /// </summary>
    Task ExecuteAsync(Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Spawns coins on an already-loaded <paramref name="game"/>, advances phase, and saves.
    /// </summary>
    Task ExecuteForGameAsync(Game game, CancellationToken cancellationToken = default);
}
