namespace ScrambleCoin.Application.Services;

/// <summary>
/// Configuration options for the matchmaking queue.
/// Bind from the <c>"Queue"</c> configuration section.
/// </summary>
public sealed class QueueOptions
{
    /// <summary>
    /// Minutes before a waiting queue entry is considered timed out.
    /// Default: 5 minutes.
    /// </summary>
    public int TimeoutMinutes { get; init; } = 5;
}
