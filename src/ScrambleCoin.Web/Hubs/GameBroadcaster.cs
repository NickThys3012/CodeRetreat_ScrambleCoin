using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using ScrambleCoin.Application.Abstractions;
using ScrambleCoin.Application.Games.GetBoardState;

namespace ScrambleCoin.Web.Hubs;

/// <summary>
/// Implements <see cref="IGameBroadcaster"/> by pushing SignalR events to the
/// <c>game-{gameId}</c> group in <see cref="GameHub"/>.
/// Called from the Application layer via the <see cref="IGameBroadcaster"/> abstraction
/// to keep Application free from SignalR dependencies.
/// </summary>
public sealed class GameBroadcaster : IGameBroadcaster
{
    private const string GameGroupPrefix   = "game-";
    private const string PlayerGroupPrefix = "player-";

    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IHubContext<GameHub> _hubContext;
    private readonly ISender _sender;

    public GameBroadcaster(IHubContext<GameHub> hubContext, ISender sender)
    {
        _hubContext = hubContext;
        _sender     = sender;
    }

    /// <inheritdoc />
    public async Task<BroadcastResult?> BroadcastBoardStateAsync(Guid gameId, CancellationToken ct = default)
    {
        var boardState = await _sender.Send(new GetSpectatorBoardStateQuery(gameId), ct);
        if (boardState is null) return null;
        var group = _hubContext.Clients.Group(GameGroupPrefix + gameId);
        await group.SendAsync("BoardStateUpdated", boardState, ct);
        var json = JsonSerializer.Serialize(boardState, _json);
        return new BroadcastResult(boardState.Turn, boardState.Phase ?? "Unknown", json);
    }

    /// <inheritdoc />
    public async Task BroadcastPhaseChangedAsync(
        Guid gameId,
        int turn,
        string? previousPhase,
        string? newPhase,
        CancellationToken ct = default)
    {
        var group = _hubContext.Clients.Group(GameGroupPrefix + gameId);
        await group.SendAsync("PhaseChanged", new
        {
            GameId = gameId,
            Turn = turn,
            PreviousPhase = previousPhase,
            NewPhase = newPhase
        }, ct);
    }

    /// <inheritdoc />
    public async Task BroadcastGameEndedAsync(
        Guid gameId,
        int playerOneScore,
        int playerTwoScore,
        Guid? winnerId,
        bool isDraw,
        CancellationToken ct = default)
    {
        var group = _hubContext.Clients.Group(GameGroupPrefix + gameId);
        await group.SendAsync("GameEnded", new
        {
            GameId = gameId,
            PlayerOneScore = playerOneScore,
            PlayerTwoScore = playerTwoScore,
            WinnerId = winnerId,
            IsDraw = isDraw
        }, ct);
    }

    /// <inheritdoc />
    public async Task NotifyActivePlayersAsync(Guid gameId, CancellationToken ct = default)
    {
        var info = await _sender.Send(new GetGamePlayerIdsQuery(gameId), ct);

        switch (info.Phase)
        {
            case "PlacePhase":
                // Both players need to place (or skip) — notify each on their private channel.
                await SendActionRequiredAsync(gameId, info.PlayerOne, "PlacePhase", ct);
                await SendActionRequiredAsync(gameId, info.PlayerTwo, "PlacePhase", ct);
                break;

            case "MovePhase" when info.ActiveMover is { } activeId:
                await SendActionRequiredAsync(gameId, activeId, "MovePhase", ct);
                break;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private Task SendActionRequiredAsync(Guid gameId, Guid playerId, string phase, CancellationToken ct)
    {
        var group = _hubContext.Clients.Group(PlayerGroupPrefix + gameId + "-" + playerId);
        return group.SendAsync("ActionRequired", new { Phase = phase }, ct);
    }
}
