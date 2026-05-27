namespace ScrambleCoin.Application.Services;

/// <summary>
/// In-memory matchmaking queue for bots.
/// </summary>
public interface IQueueService
{
    /// <summary>
    /// Enqueues a bot with its lineup for matchmaking.
    /// </summary>
    /// <param name="lineupPieceNames">Ordered a list of exactly 5 piece names.</param>
    /// <param name="botToken">
    /// Optional bearer token identifying the bot.  When provided:
    /// <list type="bullet">
    ///   <item>If the bot is already waiting in the queue → returns <c>Status="conflict"</c> (409).</item>
    ///   <item>If the bot already has an active (InProgress) game → returns <c>Status="conflict"</c> (409).</item>
    /// </list>
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="QueueEntry"/> with:
    /// <list type="bullet">
    ///   <item><c>Status="matched"</c> (200 OK) if another bot was waiting,</item>
    ///   <item><c>Status="waiting"</c> (202 Accepted) if this bot is now first in the queue, or</item>
    ///   <item><c>Status="conflict"</c> (409 Conflict) if the bot is already queued or in an active game.</item>
    /// </list>
    /// </returns>
    public Task<QueueEntry> EnqueueAsync(
        IReadOnlyList<string> lineupPieceNames,
        Guid? botToken = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Polls for the current status of a queue entry.
    /// </summary>
    /// <param name="queueId">The queue ID returned by <see cref="EnqueueAsync"/>.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    /// The <see cref="QueueEntry"/> (status may be <c>"waiting"</c>, <c>"matched"</c>, or <c>"timed_out"</c>),
    /// or <c>null</c> if the ID is not recognized.
    /// </returns>
    public Task<QueueEntry?> PollAsync(Guid queueId, CancellationToken cancellationToken = default);
}
