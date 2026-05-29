using Microsoft.AspNetCore.SignalR;

namespace ScrambleCoin.Web.Hubs;

/// <summary>
/// SignalR hub that allows spectators to subscribe to live game updates.
/// Spectators join a game-specific group to receive board state pushes.
/// </summary>
/// <remarks>
/// Client method names pushed by the server:
/// <list type="bullet">
///   <item><c>BoardStateUpdated</c> — full board state after every move/placement/coin-spawn.</item>
///   <item><c>PhaseChanged</c>      — phase transition info (turn, previousPhase, newPhase).</item>
///   <item><c>GameEnded</c>         — final scores and winner when the game finishes.</item>
/// </list>
/// </remarks>
public sealed class GameHub : Hub
{
    private const string GroupPrefix = "game-";

    /// <summary>
    /// Adds the calling client to the spectator group for <paramref name="gameId"/>.
    /// The caller will receive all later push events for that game.
    /// </summary>
    /// <param name="gameId">The game to spectate (as a string GUID).</param>
    public async Task JoinGame(string gameId)
    {
        var groupName = GroupPrefix + gameId;
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Removes the calling client from the spectator group for <paramref name="gameId"/>.
    /// </summary>
    /// <param name="gameId">The game to stop spectating.</param>
    public async Task LeaveGame(string gameId)
    {
        var groupName = GroupPrefix + gameId;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Registers the caller as an active player for <paramref name="gameId"/>.
    /// The connection is added to a private group <c>player-{gameId}-{playerId}</c>
    /// that only receives <c>ActionRequired</c> messages targeted at that specific player.
    /// Call this once after joining a game — no token validation is performed here because
    /// <c>ActionRequired</c> carries no secret data; all real actions still require a valid
    /// <c>X-Bot-Token</c> on the REST API.
    /// </summary>
    /// <param name="gameId">The game the caller is participating in (string GUID).</param>
    /// <param name="playerId">The caller's player ID (string GUID).</param>
    public async Task RegisterAsPlayer(string gameId, string playerId)
    {
        var groupName = $"player-{gameId}-{playerId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }
}
