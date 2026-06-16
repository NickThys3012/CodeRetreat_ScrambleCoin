using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Abstractions;
using ScrambleCoin.Application.Games;
using ScrambleCoin.Application.Games.Replay;

namespace ScrambleCoin.Application.Behaviours;

/// <summary>
/// MediatR pipeline behaviour that broadcasts the updated board state to all SignalR spectators
/// after every <see cref="IGameStateChangingCommand"/> succeeds, then captures a replay snapshot.
/// <para>
/// Broadcast and snapshot failures are logged and swallowed — they must not cause a command to fail.
/// </para>
/// </summary>
public sealed class SignalRBroadcastBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IGameBroadcaster _broadcaster;
    private readonly IGameSnapshotRepository _snapshots;
    private readonly ILogger<SignalRBroadcastBehaviour<TRequest, TResponse>> _logger;

    public SignalRBroadcastBehaviour(
        IGameBroadcaster broadcaster,
        IGameSnapshotRepository snapshots,
        ILogger<SignalRBroadcastBehaviour<TRequest, TResponse>> logger)
    {
        _broadcaster = broadcaster;
        _snapshots = snapshots;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next(cancellationToken);

        if (request is not IGameStateChangingCommand gameCommand)
            return response;

        try
        {
            var result = await _broadcaster.BroadcastBoardStateAsync(gameCommand.GameId, cancellationToken);
            await _broadcaster.NotifyActivePlayersAsync(gameCommand.GameId, cancellationToken);

            if (result is not null)
                await CaptureSnapshotAsync(gameCommand.GameId, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "SignalR broadcast failed for game {GameId} after {CommandType}. Spectators may miss an update.",
                gameCommand.GameId,
                typeof(TRequest).Name);
        }

        return response;
    }

    private async Task CaptureSnapshotAsync(Guid gameId, BroadcastResult result)
    {
        try
        {
            // Use CancellationToken.None — the HTTP request token is already cancelled
            // by the time an unawaited task would run, and we don't want to lose the snapshot.
            await _snapshots.SaveSnapshotAsync(gameId, result.Turn, result.Phase, result.BoardStateJson, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Snapshot capture failed for game {GameId} turn {Turn} — replay may be incomplete.", gameId, result.Turn);
        }
    }
}
