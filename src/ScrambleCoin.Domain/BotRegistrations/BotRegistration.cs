namespace ScrambleCoin.Domain.BotRegistrations;

/// <summary>
/// Represents an authenticated bot player registered to a game session.
/// </summary>
/// <remarks>
/// <see cref="Token"/> is the bearer credential the bot must supply on every subsequent
/// game-action endpoint.  <see cref="PlayerId"/> is the slot ID returned at join-time
/// and used for all MediatR commands (PlacePiece, MovePiece, etc.).
/// </remarks>
public sealed class BotRegistration
{
    /// <summary>Opaque bearer token — primary key.</summary>
    public Guid Token { get; }

    /// <summary>The player-slot identifier assigned to this bot inside the game aggregate.</summary>
    public Guid PlayerId { get; }

    /// <summary>The game this bot is registered to.</summary>
    public Guid GameId { get; }

    public BotRegistration(Guid token, Guid playerId, Guid gameId)
    {
        Token = token;
        PlayerId = playerId;
        GameId = gameId;
    }
}
