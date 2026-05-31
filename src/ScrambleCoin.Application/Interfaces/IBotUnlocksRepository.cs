using ScrambleCoin.Domain.Entities;

namespace ScrambleCoin.Application.Interfaces;

/// <summary>
/// Provides persistence operations for <see cref="BotUnlock"/> records.
/// </summary>
public interface IBotUnlocksRepository
{
    /// <summary>Gets all villains defeated by a specific bot.</summary>
    Task<IEnumerable<BotUnlock>> GetDefeatedVillainsAsync(Guid botId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all pieces unlocked by a bot (includes starter pieces and defeated villain rewards).
    /// Returns the piece IDs that are available to this bot.
    /// </summary>
    Task<IEnumerable<string>> GetUnlockedPieceIdsAsync(Guid botId, CancellationToken cancellationToken = default);

    /// <summary>Records that a bot defeated a specific villain and unlocked a piece.</summary>
    /// <exception cref="InvalidOperationException">Thrown if the bot has already defeated this villain.</exception>
    Task RecordDefeatAsync(Guid botId, string villainId, string? unlockedPieceId, CancellationToken cancellationToken = default);

    /// <summary>Checks if a bot has already defeated a specific villain.</summary>
    Task<bool> HasDefeatedVillainAsync(Guid botId, string villainId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all bot-unlock records across every bot, used by the admin panel
    /// to render the global solo-mode progress view.
    /// </summary>
    Task<IEnumerable<BotUnlock>> GetAllAsync(CancellationToken cancellationToken = default);
}
