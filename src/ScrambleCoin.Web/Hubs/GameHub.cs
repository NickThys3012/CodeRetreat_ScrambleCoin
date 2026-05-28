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
    /// All subsequent push events for that game will be received by the caller.
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
}
