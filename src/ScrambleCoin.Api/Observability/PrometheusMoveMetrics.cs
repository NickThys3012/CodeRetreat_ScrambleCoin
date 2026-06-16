using Prometheus;
using ScrambleCoin.Application.Abstractions;

namespace ScrambleCoin.Api.Observability;

/// <summary>
/// <c>prometheus-net</c> implementation of <see cref="IMoveMetrics"/>. Lives in the Api layer so the
/// Application layer stays free of the metrics dependency. Increments the process-wide
/// <c>scramblecoin_moves_total</c> counter, labelled by <c>game_id</c> and <c>player_id</c>.
/// </summary>
public sealed class PrometheusMoveMetrics : IMoveMetrics
{
    private static readonly Counter MoveCounter = Metrics.CreateCounter(
        "scramblecoin_moves_total",
        "Total moves submitted",
        new CounterConfiguration { LabelNames = new[] { "game_id", "player_id" } });

    public void RecordMove(Guid gameId, Guid playerId)
        => MoveCounter.WithLabels(gameId.ToString(), playerId.ToString()).Inc();
}
