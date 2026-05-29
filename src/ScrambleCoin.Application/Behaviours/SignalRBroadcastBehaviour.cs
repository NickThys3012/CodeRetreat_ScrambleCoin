using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Abstractions;
using ScrambleCoin.Application.Games;

namespace ScrambleCoin.Application.Behaviours;

/// <summary>
/// MediatR pipeline behaviour that broadcasts the updated board state to all SignalR spectators
/// after every <see cref="IGameStateChangingCommand"/> succeeds.
/// <para>
/// Broadcast failures are logged and swallowed — they must not cause a command to fail.
/// </para>
/// </summary>
public sealed class SignalRBroadcastBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IGameBroadcaster _broadcaster;
    private readonly ILogger<SignalRBroadcastBehaviour<TRequest, TResponse>> _logger;

    public SignalRBroadcastBehaviour(
        IGameBroadcaster broadcaster,
        ILogger<SignalRBroadcastBehaviour<TRequest, TResponse>> logger)
    {
        _broadcaster = broadcaster;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Execute the command first; broadcast only on success.
        var response = await next(cancellationToken);

        if (request is not IGameStateChangingCommand gameCommand)
        {
            return response;
        }
        try
        {
            await _broadcaster.BroadcastBoardStateAsync(gameCommand.GameId, cancellationToken);
            await _broadcaster.NotifyActivePlayersAsync(gameCommand.GameId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "SignalR broadcast failed for game {GameId} after {CommandType}. Spectators may miss an update.",
                gameCommand.GameId,
                typeof(TRequest).Name);
            // Do NOT re-throw — a broadcast failure must never fail the command.
        }

        return response;
    }
}
