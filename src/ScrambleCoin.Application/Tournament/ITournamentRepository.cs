using ScrambleCoin.Domain.Tournaments;

namespace ScrambleCoin.Application.Tournament;

/// <summary>
/// Persistence operations for <see cref="Domain.Tournaments.Tournament"/> aggregates.
/// </summary>
public interface ITournamentRepository
{
    /// <summary>Retrieves a tournament by its unique identifier.</summary>
    /// <exception cref="ScrambleCoin.Domain.Exceptions.TournamentNotFoundException">Thrown when not found.</exception>
    Task<Domain.Tournaments.Tournament> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Persists the current state of a tournament (insert or update).</summary>
    Task SaveAsync(Domain.Tournaments.Tournament tournament, CancellationToken cancellationToken = default);
}
