namespace ScrambleCoin.Domain.Exceptions;

/// <summary>
/// Thrown when a result is requested for a game that has not yet finished.
/// </summary>
public sealed class GameNotFinishedException : DomainException
{
    public Guid GameId { get; }

    public GameNotFinishedException(Guid gameId)
        : base($"Game {gameId} has not finished yet.") =>
        GameId = gameId;
}
