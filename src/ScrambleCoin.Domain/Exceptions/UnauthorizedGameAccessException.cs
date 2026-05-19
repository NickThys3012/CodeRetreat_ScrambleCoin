namespace ScrambleCoin.Domain.Exceptions;

/// <summary>
/// Thrown when a bot token does not match any registered player for the requested game.
/// </summary>
public sealed class UnauthorizedGameAccessException : DomainException
{
    public UnauthorizedGameAccessException()
        : base("Bot token is not authorized for this game.") { }
}
