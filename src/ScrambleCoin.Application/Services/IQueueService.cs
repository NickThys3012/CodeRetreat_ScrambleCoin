namespace ScrambleCoin.Application.Services;

/// <summary>
/// In-memory matchmaking queue for bots.
/// </summary>
public interface IQueueService
{
    /// <summary>
    /// Enqueues a bot with its lineup for matchmaking.
    /// </summary>
    /// <param name="lineupPieceNames">Ordered list of exactly 5 piece names.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="QueueEntry"/> with <c>Status="matched"</c> (200 OK) if another bot was waiting,
    /// or <c>Status="waiting"</c> (202 Accepted) if this bot is now first in the queue.
    /// </returns>
    Task<QueueEntry> EnqueueAsync(IReadOnlyList<string> lineupPieceNames, CancellationToken cancellationToken = default);

    /// <summary>
    /// Polls for the current status of a queue entry.
    /// </summary>
    /// <param name="queueId">The queue ID returned by <see cref="EnqueueAsync"/>.</param>
    /// <returns>The <see cref="QueueEntry"/>, or <c>null</c> if the ID is not recognised.</returns>
    Task<QueueEntry?> PollAsync(Guid queueId, CancellationToken cancellationToken = default);
}
