using MediatR;
using Microsoft.AspNetCore.SignalR;
using ScrambleCoin.Application.Games.GetBoardState;

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

    private readonly ISender _sender;

    public GameHub(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Adds the caller to the spectator group for <paramref name="gameId"/>.</summary>
    public async Task JoinGame(string gameId) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, GameGroupPrefix + gameId);

    /// <summary>Removes the caller from the spectator group for <paramref name="gameId"/>.</summary>
    public async Task LeaveGame(string gameId) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GameGroupPrefix + gameId);

    /// <summary>
    /// Registers the caller as an active player so they receive <c>ActionRequired</c>
    /// events only when it is their turn to act.
    /// A catch-up <c>ActionRequired</c> is sent immediately if the player needs to act right now,
    /// so bots that connect after the initial event was broadcast don't stall.
    /// </summary>
    public async Task RegisterAsPlayer(string gameId, string playerId)
    {
        if (!Guid.TryParse(gameId, out var gameGuid) || !Guid.TryParse(playerId, out var playerGuid))
            return;

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            PlayerGroupPrefix + gameId + "-" + playerId);

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
