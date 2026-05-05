namespace ScrambleCoin.Domain.Exceptions;

public sealed class PlayerAlreadyActedException : DomainException
{
    public PlayerAlreadyActedException(Guid playerId)
        : base($"Player {playerId} has already acted during the Place Phase this turn.") { }
}
