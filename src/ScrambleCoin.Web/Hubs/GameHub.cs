using MediatR;
using Microsoft.AspNetCore.SignalR;
using ScrambleCoin.Application.Games.GetBoardState;

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
///   <item><c>ActionRequired</c>    — sent to a specific player's private group when they must act.</item>
/// </list>
/// </remarks>
public sealed class GameHub : Hub
{
    private const string GroupPrefix = "game-";
    private readonly ISender _sender;

    public GameHub(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Adds the calling client to the spectator group for <paramref name="gameId"/>.
    /// The caller will receive all later push events for that game.
    /// </summary>
    public async Task JoinGame(string gameId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupPrefix + gameId);
    }

    /// <summary>
    /// Removes the calling client from the spectator group for <paramref name="gameId"/>.
    /// </summary>
    public async Task LeaveGame(string gameId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupPrefix + gameId);
    }

    /// <summary>
    /// Registers the caller as an active player for <paramref name="gameId"/>.
    /// The connection is added to a private group <c>player-{gameId}-{playerId}</c>
    /// that only receives <c>ActionRequired</c> messages targeted at that specific player.
    /// A catch-up <c>ActionRequired</c> is sent immediately if the player needs to act right now,
    /// so bots that connect after the initial event was broadcast don't stall.
    /// </summary>
    public async Task RegisterAsPlayer(string gameId, string playerId)
    {
        if (!Guid.TryParse(gameId, out var gameGuid) || !Guid.TryParse(playerId, out var playerGuid))
            return;

        var groupName = $"player-{gameId}-{playerId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        // Catch-up: if the player needs to act right now, send ActionRequired immediately.
        try
        {
            var info = await _sender.Send(new GetGamePlayerIdsQuery(gameGuid));
            var needsToAct = info.Phase switch
            {
                "PlacePhase" => info.PlayerOne == playerGuid || info.PlayerTwo == playerGuid,
                "MovePhase"  => info.ActiveMover == playerGuid,
                _            => false
            };

            if (needsToAct)
                await Clients.Caller.SendAsync("ActionRequired", new {
                    info.Phase });
        }
        catch
        {
            // Best-effort catch-up — don't fail the registration itself.
        }
    }
}

