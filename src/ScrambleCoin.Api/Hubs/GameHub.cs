using Microsoft.AspNetCore.SignalR;

namespace ScrambleCoin.Api.Hubs;

/// <summary>
/// SignalR hub hosted by the <c>ScrambleCoin.Api</c> project.
/// Identical in protocol to the one in <c>ScrambleCoin.Web</c> — same group names,
/// same client-method names — so bots can connect to whichever host they target.
/// </summary>
public sealed class GameHub : Hub
{
    private const string GameGroupPrefix   = "game-";
    private const string PlayerGroupPrefix = "player-";

    /// <summary>Adds the caller to the spectator group for <paramref name="gameId"/>.</summary>
    public async Task JoinGame(string gameId) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, GameGroupPrefix + gameId);

    /// <summary>Removes the caller from the spectator group for <paramref name="gameId"/>.</summary>
    public async Task LeaveGame(string gameId) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GameGroupPrefix + gameId);

    /// <summary>
    /// Registers the caller as an active player so they receive <c>ActionRequired</c>
    /// events only when it is their turn to act.
    /// </summary>
    public async Task RegisterAsPlayer(string gameId, string playerId) =>
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            PlayerGroupPrefix + gameId + "-" + playerId);
}
