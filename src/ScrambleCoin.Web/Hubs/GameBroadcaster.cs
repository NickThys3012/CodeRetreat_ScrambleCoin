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
    private const string GroupPrefix = "game-";

    private readonly IHubContext<GameHub> _hubContext;
    private readonly ISender _sender;

    public GameBroadcaster(IHubContext<GameHub> hubContext, ISender sender)
    {
        _hubContext = hubContext;
        _sender = sender;
    }

    /// <inheritdoc />
    public async Task BroadcastBoardStateAsync(Guid gameId, CancellationToken ct = default)
    {
        var boardState = await _sender.Send(new GetSpectatorBoardStateQuery(gameId), ct);
        var group = _hubContext.Clients.Group(GroupPrefix + gameId);
        await group.SendAsync("BoardStateUpdated", boardState, ct);
    }

    /// <inheritdoc />
    public async Task BroadcastPhaseChangedAsync(
        Guid gameId,
        int turn,
        string? previousPhase,
        string? newPhase,
        CancellationToken ct = default)
    {
        var group = _hubContext.Clients.Group(GroupPrefix + gameId);
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
        var group = _hubContext.Clients.Group(GroupPrefix + gameId);
        await group.SendAsync("GameEnded", new
        {
            GameId = gameId,
            PlayerOneScore = playerOneScore,
            PlayerTwoScore = playerTwoScore,
            WinnerId = winnerId,
            IsDraw = isDraw
        }, ct);
    }
}
