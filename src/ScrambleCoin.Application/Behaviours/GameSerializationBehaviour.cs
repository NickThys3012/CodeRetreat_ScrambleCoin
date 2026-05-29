using MediatR;
using ScrambleCoin.Application.Games;
using ScrambleCoin.Application.Services;

namespace ScrambleCoin.Application.Behaviours;

/// <summary>
/// MediatR pipeline behaviour that serializes concurrent mutations of the same game.
/// <para>
/// When two bots submit actions for the same game simultaneously, without this behaviour
/// both requests read the same game state before either saves — the second write silently
/// overwrites the first (lost-update race) and the phase never advances.
/// </para>
/// <para>
/// This behaviour acquires a per-game <see cref="System.Threading.SemaphoreSlim"/> before
/// calling the next handler, so only one <see cref="IGameStateChangingCommand"/> per game
/// executes at a time. Villain commands dispatched from within a player command (solo mode)
/// are detected via <see cref="AsyncLocal{T}"/> and skip re-acquisition to avoid deadlock.
/// </para>
/// </summary>
public sealed class GameSerializationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Tracks which game IDs the current async call-chain already holds a lock for,
    // so that re-entrant villain commands don't deadlock.
    private static readonly AsyncLocal<HashSet<Guid>?> LockedGames = new();

    private readonly GameLockService _lockService;

    public GameSerializationBehaviour(GameLockService lockService)
    {
        _lockService = lockService;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IGameStateChangingCommand gameCommand)
            return await next(cancellationToken);

        // Initialise the per-context set on first use.
        LockedGames.Value ??= [];

        // Re-entrant: already locked in this async chain (e.g. villain command inside player handler).
        if (LockedGames.Value.Contains(gameCommand.GameId))
            return await next(cancellationToken);

        var semaphore = _lockService.GetLock(gameCommand.GameId);
        await semaphore.WaitAsync(cancellationToken);
        LockedGames.Value.Add(gameCommand.GameId);
        try
        {
            return await next(cancellationToken);
        }
        finally
        {
            LockedGames.Value.Remove(gameCommand.GameId);
            semaphore.Release();
        }
    }
}
