namespace ScrambleCoin.Application.Services;

/// <summary>
/// Represents one bot's entry in the matchmaking queue.
/// </summary>
/// <param name="QueueId">Unique identifier for this queue entry (returned to the bot as a polling handle).</param>
/// <param name="Status">One of: <c>"waiting"</c>, <c>"matched"</c>, <c>"conflict"</c>, or <c>"timed_out"</c>.</param>
/// <param name="GameId">Set when <see cref="Status"/> is <c>"matched"</c>.</param>
/// <param name="PlayerId">Set when <see cref="Status"/> is <c>"matched"</c>.</param>
/// <param name="Token">Bearer token set when <see cref="Status"/> is <c>"matched"</c>.</param>
public sealed record QueueEntry(
    Guid QueueId,
    string Status,
    Guid? GameId = null,
    Guid? PlayerId = null,
    Guid? Token = null);
