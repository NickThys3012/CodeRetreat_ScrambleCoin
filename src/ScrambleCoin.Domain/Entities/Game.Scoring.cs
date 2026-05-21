using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;

namespace ScrambleCoin.Domain.Entities;

/// <summary>
/// Score tracking and coin event handling.
/// Manages scoring operations and coin-related domain events (collection and theft).
/// </summary>
public partial class Game
{
    // ── Scoring ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Increments <paramref name="playerId"/>'s score by <paramref name="points"/>.
    /// </summary>
    /// <exception cref="DomainException">
    /// Thrown when the game is not <see cref="GameStatus.InProgress"/>,
    /// when <paramref name="playerId"/> is not a participant,
    /// or when <paramref name="points"/> is negative.
    /// </exception>
    public void AddScore(Guid playerId, int points)
    {
        if (Status != GameStatus.InProgress)
            throw new DomainException(
                $"Scores can only be updated while the game is {GameStatus.InProgress}. Current status: {Status}.");

        if (points < 0)
            throw new DomainException($"Points must be non-negative, but was {points}.");

        if (!_scores.ContainsKey(playerId))
            throw new DomainException($"Player {playerId} is not a participant of game {Id}.");

        _scores[playerId] += points;
    }
}
