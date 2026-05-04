namespace ScrambleCoin.Domain.Exceptions;

/// <summary>
/// Thrown when a bot attempts to join a game that already has two registered players.
/// Maps to HTTP 409 Conflict at the API layer.
/// </summary>
public sealed class GameFullException : DomainException
{
    public Guid GameId { get; }

    public GameFullException(Guid gameId)
        : base($"Game {gameId} already has two registered players and cannot accept more.")
    {
        GameId = gameId;
    }
}
