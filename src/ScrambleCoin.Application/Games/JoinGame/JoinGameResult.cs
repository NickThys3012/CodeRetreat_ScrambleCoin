namespace ScrambleCoin.Application.Games.JoinGame;

/// <summary>
/// Result returned to the bot after successfully joining a game.
/// </summary>
/// <param name="PlayerId">
/// The player-slot identifier the bot must use for all subsequent game actions
/// (PlacePiece, MovePiece, etc.).
/// </param>
/// <param name="Token">The bearer token the bot must supply on every subsequent request.</param>
public sealed record JoinGameResult(Guid PlayerId, Guid Token);
