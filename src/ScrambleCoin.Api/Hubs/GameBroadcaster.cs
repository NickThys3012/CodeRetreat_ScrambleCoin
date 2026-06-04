using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using ScrambleCoin.Application.Abstractions;
using ScrambleCoin.Application.Games.GetBoardState;

namespace ScrambleCoin.Api.Hubs;

/// <summary>
/// <see cref="IGameBroadcaster"/> implementation for the <c>ScrambleCoin.Api</c> host.
/// Identical logic to the one in <c>ScrambleCoin.Web</c> but wired to
/// <see cref="GameHub"/> (the Api project's own hub type).
/// </summary>
internal sealed class GameBroadcaster : IGameBroadcaster
{
    private const string GameGroupPrefix   = "game-";
    private const string PlayerGroupPrefix = "player-";

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IHubContext<GameHub> _hubContext;
    private readonly ISender _sender;

    public GameBroadcaster(IHubContext<GameHub> hubContext, ISender sender)
    {
        _hubContext = hubContext;
        _sender     = sender;
    }

    public async Task<BroadcastResult?> BroadcastBoardStateAsync(Guid gameId, CancellationToken ct = default)
    {
        var boardState = await _sender.Send(new GetSpectatorBoardStateQuery(gameId), ct);
        await _hubContext.Clients.Group(GameGroupPrefix + gameId)
            .SendAsync("BoardStateUpdated", boardState, ct);
        var json = JsonSerializer.Serialize(boardState, Json);
        return new BroadcastResult(boardState.Turn, boardState.Phase ?? "Unknown", json);
    }

    public async Task BroadcastPhaseChangedAsync(
        Guid gameId, int turn, string? previousPhase, string? newPhase,
        CancellationToken ct = default)
    {
        await _hubContext.Clients.Group(GameGroupPrefix + gameId)
            .SendAsync("PhaseChanged", new
            {
                GameId        = gameId,
                Turn          = turn,
                PreviousPhase = previousPhase,
                NewPhase      = newPhase
            }, ct);
    }

    public async Task BroadcastGameEndedAsync(
        Guid gameId, int playerOneScore, int playerTwoScore, Guid? winnerId, bool isDraw,
        CancellationToken ct = default)
    {
        await _hubContext.Clients.Group(GameGroupPrefix + gameId)
            .SendAsync("GameEnded", new
            {
                GameId         = gameId,
                PlayerOneScore = playerOneScore,
                PlayerTwoScore = playerTwoScore,
                WinnerId       = winnerId,
                IsDraw         = isDraw
            }, ct);
    }

    public async Task NotifyActivePlayersAsync(Guid gameId, CancellationToken ct = default)
    {
        var info = await _sender.Send(new GetGamePlayerIdsQuery(gameId), ct);

        switch (info.Phase)
        {
            case "PlacePhase":
                await SendActionRequiredAsync(gameId, info.PlayerOne, "PlacePhase", ct);
                await SendActionRequiredAsync(gameId, info.PlayerTwo, "PlacePhase", ct);
                break;

            case "MovePhase" when info.ActiveMover is { } activeId:
                await SendActionRequiredAsync(gameId, activeId, "MovePhase", ct);
                break;
        }
    }

    private Task SendActionRequiredAsync(Guid gameId, Guid playerId, string phase, CancellationToken ct)
    {
        var group = _hubContext.Clients.Group(PlayerGroupPrefix + gameId + "-" + playerId);
        return group.SendAsync("ActionRequired", new { Phase = phase }, ct);
    }
}
