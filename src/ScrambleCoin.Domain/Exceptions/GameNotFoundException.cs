namespace ScrambleCoin.Domain.Exceptions;

public class GameNotFoundException : DomainException
{
    public Guid GameId { get; }

    public GameNotFoundException(Guid gameId)
        : base($"Game {gameId} was not found.") { GameId = gameId; }
}
