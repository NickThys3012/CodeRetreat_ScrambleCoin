namespace ScrambleCoin.Application.Abstractions;

/// <summary>
/// Abstraction for recording move-submission metrics. Implemented in the Api layer with
/// <c>prometheus-net</c> so the Application layer stays free of any metrics-library dependency
/// (it keeps referencing only Domain + MediatR).
/// </summary>
public interface IMoveMetrics
{
    /// <summary>
    /// Records a single successfully-committed move for the given game and player.
    /// Surfaces as the <c>scramblecoin_moves_total</c> counter (labelled by game/player).
    /// </summary>
    void RecordMove(Guid gameId, Guid playerId);
}
