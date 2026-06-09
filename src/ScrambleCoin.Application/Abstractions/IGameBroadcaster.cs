namespace ScrambleCoin.Application.Abstractions;

/// <summary>
/// Abstraction for broadcasting game events to connected spectators via SignalR.
/// Implemented in the Web layer to keep the Application layer free from SignalR dependencies.
/// </summary>
public interface IGameBroadcaster
{
    /// <summary>
    /// Fetches the current board state, broadcasts a <c>BoardStateUpdated</c> message
    /// to all spectators watching <paramref name="gameId"/>, and returns the serialized
    /// board-state JSON for snapshot capture. Returns <c>null</c> when no state is available.
    /// </summary>
    Task<BroadcastResult?> BroadcastBoardStateAsync(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Broadcasts a <c>PhaseChanged</c> message to all spectators watching <paramref name="gameId"/>.
    /// </summary>
    Task BroadcastPhaseChangedAsync(Guid gameId, int turn, string? previousPhase, string? newPhase, CancellationToken ct = default);

    /// <summary>
    /// Broadcasts a <c>GameEnded</c> message with final scores and winner info
    /// to all spectators watching <paramref name="gameId"/>.
    /// </summary>
    Task BroadcastGameEndedAsync(Guid gameId, int playerOneScore, int playerTwoScore, Guid? winnerId, bool isDraw, CancellationToken ct = default);

    /// <summary>
    /// Sends an <c>ActionRequired</c> message to the player(s) who need to act right now.
    /// <list type="bullet">
    ///   <item>During <c>PlacePhase</c> — notifies <em>both</em> players (each must place or skip).</item>
    ///   <item>During <c>MovePhase</c>  — notifies only the <em>active mover</em>.</item>
    ///   <item>Other phases — no notification is sent.</item>
    /// </list>
    /// Bots register for these targeted messages by calling <c>RegisterAsPlayer</c> on the hub.
    /// </summary>
    Task NotifyActivePlayersAsync(Guid gameId, CancellationToken ct = default);
}

/// <summary>
/// Data returned by <see cref="IGameBroadcaster.BroadcastBoardStateAsync"/> so the caller
/// can capture a replay snapshot without an extra DB round-trip.
/// </summary>
public sealed record BroadcastResult(int Turn, string Phase, string BoardStateJson);
