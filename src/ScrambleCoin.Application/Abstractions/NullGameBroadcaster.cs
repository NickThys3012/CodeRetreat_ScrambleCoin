namespace ScrambleCoin.Application.Abstractions;

/// <summary>
/// No-op <see cref="IGameBroadcaster"/> for hosts that do not serve SignalR connections
/// (e.g. the standalone <c>ScrambleCoin.Api</c> host).
/// Registered so the <c>SignalRBroadcastBehaviour</c> pipeline resolves without error;
/// all methods are silent no-ops.
/// </summary>
/// <remarks>
/// For real-time spectator updates, bots should target the <c>ScrambleCoin.Web</c> host,
/// which registers <c>GameBroadcaster</c> (the live SignalR implementation).
/// </remarks>
public sealed class NullGameBroadcaster : IGameBroadcaster
{
    public Task BroadcastBoardStateAsync(Guid gameId, CancellationToken ct = default) => Task.CompletedTask;

    public Task BroadcastPhaseChangedAsync(Guid gameId, int turn, string? previousPhase, string? newPhase, CancellationToken ct = default) => Task.CompletedTask;

    public Task BroadcastGameEndedAsync(Guid gameId, int playerOneScore, int playerTwoScore, Guid? winnerId, bool isDraw, CancellationToken ct = default) => Task.CompletedTask;
}
