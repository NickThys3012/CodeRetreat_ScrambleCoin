namespace ScrambleCoin.Application.Tournament;

/// <summary>
/// Stable bot identity information resolved from a tournament match.
/// </summary>
/// <param name="BotOneId">Stable bot ID of the first participant.</param>
/// <param name="BotOneName">Display name of the first participant.</param>
/// <param name="BotOnePlayerId">Per-game player slot ID for the first participant; <c>null</c> if not yet assigned.</param>
/// <param name="BotTwoId">Stable bot ID of the second participant.</param>
/// <param name="BotTwoName">Display name of the second participant.</param>
/// <param name="BotTwoPlayerId">Per-game player slot ID for the second participant; <c>null</c> if not yet assigned.</param>
public sealed record TournamentBotInfo(
    Guid BotOneId,
    string BotOneName,
    Guid? BotOnePlayerId,
    Guid BotTwoId,
    string BotTwoName,
    Guid? BotTwoPlayerId);

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

    /// <summary>
    /// Finds the stable bot identities for the two participants in a tournament match associated with
    /// the specified game, or <c>null</c> if the game is not part of any tournament.
    /// </summary>
    /// <param name="gameId">The game ID to search for across all tournament group and knockout matches.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TournamentBotInfo?> GetBotInfoByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default);
}
