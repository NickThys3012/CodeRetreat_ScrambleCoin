namespace ScrambleCoin.Application.Abstractions;

/// <summary>
/// Abstraction for broadcasting game events to connected spectators via SignalR.
/// Implemented in the Web layer to keep the Application layer free from SignalR dependencies.
/// </summary>
public interface IGameBroadcaster
{
    /// <summary>
    /// Fetches the current board state and broadcasts a <c>BoardStateUpdated</c> message
    /// to all spectators watching <paramref name="gameId"/>.
    /// </summary>
    Task BroadcastBoardStateAsync(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Broadcasts a <c>PhaseChanged</c> message to all spectators watching <paramref name="gameId"/>.
    /// </summary>
    Task BroadcastPhaseChangedAsync(Guid gameId, int turn, string? previousPhase, string? newPhase, CancellationToken ct = default);

    /// <summary>
    /// Broadcasts a <c>GameEnded</c> message with final scores and winner info
    /// to all spectators watching <paramref name="gameId"/>.
    /// </summary>
    Task BroadcastGameEndedAsync(Guid gameId, int playerOneScore, int playerTwoScore, Guid? winnerId, bool isDraw, CancellationToken ct = default);
}
