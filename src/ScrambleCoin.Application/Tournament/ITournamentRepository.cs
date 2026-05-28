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

    /// <summary>
    /// Stages the current state of a tournament for persistence (insert or update) without committing.
    /// The caller must commit the staged changes via <see cref="ScrambleCoin.Application.Interfaces.IUnitOfWork.SaveChangesAsync"/>.
    /// </summary>
    Task SaveAsync(Domain.Tournaments.Tournament tournament, CancellationToken cancellationToken = default);
}
