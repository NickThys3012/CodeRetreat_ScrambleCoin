using System.Collections.Concurrent;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Application.Games.Matchmaking;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Services;

namespace ScrambleCoin.Infrastructure.Services;

/// <summary>
/// In-memory matchmaking queue.  Registered as a singleton.
/// When a second bot enqueues, a game shell is created immediately, both bots receive
/// their player IDs and bearer tokens, and both queue entries are updated to
/// <c>Status="matched"</c>.
/// </summary>
/// <remarks>
/// Repositories are scoped services; this singleton resolves them per-operation via
/// <see cref="IServiceScopeFactory"/> to avoid captive-dependency issues.
/// </remarks>
public sealed class QueueService : IQueueService
{
    // ── Internal record for a waiting bot ─────────────────────────────────────

    private sealed record WaitingBot(Guid QueueId, IReadOnlyList<string> LineupPieceNames, Guid? BotToken);

    // ── State ─────────────────────────────────────────────────────────────────

    private readonly ConcurrentQueue<WaitingBot> _waitingQueue = new();
    private readonly ConcurrentDictionary<Guid, QueueEntry> _entries = new();

    /// <summary>Tracks bot tokens currently in the waiting queue to detect duplicate enqueues.</summary>
    private readonly ConcurrentDictionary<Guid, bool> _waitingTokens = new();

    /// <summary>Timestamp when each queue entry was created, keyed by QueueId.</summary>
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _enqueuedAt = new();

    // ── Configuration ─────────────────────────────────────────────────────────

    private readonly TimeSpan _timeout;

    // ── DI ────────────────────────────────────────────────────────────────────

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISender _sender;
    private readonly ILogger<QueueService> _logger;

    /// <param name="scopeFactory">Used to create scoped repository instances for conflict checks.</param>
    /// <param name="sender">MediatR sender used to dispatch <see cref="StartMatchCommand"/>.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="options">
    /// Queue configuration (timeout etc.).  May be <c>null</c> in unit-test contexts;
    /// defaults to 5-minute timeout when absent.
    /// </param>
    public QueueService(
        IServiceScopeFactory scopeFactory,
        ISender sender,
        ILogger<QueueService> logger,
        IOptions<QueueOptions>? options = null)
    {
        _scopeFactory = scopeFactory;
        _sender = sender;
        _logger = logger;
        _timeout = TimeSpan.FromMinutes(options?.Value.TimeoutMinutes ?? 5);
    }

    // ── IQueueService ─────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<QueueEntry> EnqueueAsync(
        IReadOnlyList<string> lineupPieceNames,
        Guid? botToken = null,
        CancellationToken cancellationToken = default)
    {
        // ── AC 6: Check for conflict before doing anything else ───────────────

        if (botToken.HasValue)
        {
            // 1. Already waiting in the queue?
            if (_waitingTokens.ContainsKey(botToken.Value))
            {
                _logger.LogWarning(
                    "Conflict: bot token {Token} is already waiting in the queue.", botToken.Value);
                return new QueueEntry(Guid.NewGuid(), Status: "conflict");
            }

            // 2. Already in an active game?
            using var conflictScope = _scopeFactory.CreateScope();
            var botRegRepo = conflictScope.ServiceProvider.GetRequiredService<IBotRegistrationRepository>();
            var gameRepo = conflictScope.ServiceProvider.GetRequiredService<IGameRepository>();

            var existing = await botRegRepo.GetByTokenAsync(botToken.Value, cancellationToken);
            if (existing is not null)
            {
                var hasActiveGame = await gameRepo.HasActiveGameAsync(existing.PlayerId, cancellationToken);
                if (hasActiveGame)
                {
                    _logger.LogWarning(
                        "Conflict: bot token {Token} (player {PlayerId}) already has an active game.",
                        botToken.Value, existing.PlayerId);
                    return new QueueEntry(Guid.NewGuid(), Status: "conflict");
                }
            }
        }

        // ── Cleanup timed-out waiting bots before attempting to pair ──────────
        // Uses lazy eviction: skip expired candidates while searching for a match.

        var queueId = Guid.NewGuid();

        // Try to find a non-expired waiting bot.
        WaitingBot? waitingBot = null;
        while (_waitingQueue.TryDequeue(out var candidate))
        {
            if (!IsExpired(candidate.QueueId))
            {
                waitingBot = candidate;
                break;
            }
            // Discard expired bot.
            _entries.TryRemove(candidate.QueueId, out _);
            _enqueuedAt.TryRemove(candidate.QueueId, out _);
            if (candidate.BotToken.HasValue)
                _waitingTokens.TryRemove(candidate.BotToken.Value, out _);
            _logger.LogInformation("Queue entry expired and evicted: QueueId={QueueId}", candidate.QueueId);
        }

        if (waitingBot is not null)
        {
            // Remove the matched waiting bot's token from the tracking set.
            if (waitingBot.BotToken.HasValue)
                _waitingTokens.TryRemove(waitingBot.BotToken.Value, out _);

            _enqueuedAt.TryRemove(waitingBot.QueueId, out _);

            // ── Match found — delegate game creation to StartMatchCommand ─────
            var result = await _sender.Send(
                new StartMatchCommand(waitingBot.LineupPieceNames, lineupPieceNames),
                cancellationToken);

            // Update the waiting bot's entry so polling returns "matched".
            var matchedOne = new QueueEntry(
                waitingBot.QueueId,
                Status: "matched",
                GameId: result.GameId,
                PlayerId: result.PlayerOneId,
                Token: result.PlayerOneToken);
            _entries[waitingBot.QueueId] = matchedOne;

            _logger.LogInformation(
                "Queue matched: GameId={GameId}, P1QueueId={P1}, P2QueueId={P2}",
                result.GameId, waitingBot.QueueId, queueId);

            // Store the second bot's entry so they can poll if their HTTP response is lost.
            var matchedTwo = new QueueEntry(
                queueId,
                Status: "matched",
                GameId: result.GameId,
                PlayerId: result.PlayerTwoId,
                Token: result.PlayerTwoToken);
            _entries[queueId] = matchedTwo;

            return matchedTwo;
        }

        // ── No match yet — park this bot and return 202 ───────────────────
        var entry = new QueueEntry(queueId, Status: "waiting");
        _entries[queueId] = entry;
        _enqueuedAt[queueId] = DateTimeOffset.UtcNow;
        _waitingQueue.Enqueue(new WaitingBot(queueId, lineupPieceNames, botToken));

        // Track the token in the waiting set (for duplicate-enqueue detection).
        if (botToken.HasValue)
            _waitingTokens[botToken.Value] = true;

        _logger.LogInformation("Bot queued, waiting for opponent: QueueId={QueueId}", queueId);

        return entry;
    }

    /// <inheritdoc />
    public Task<QueueEntry?> PollAsync(Guid queueId, CancellationToken cancellationToken = default)
    {
        if (!_entries.TryGetValue(queueId, out var entry))
            return Task.FromResult<QueueEntry?>(null);

        // ── AC 7: Check for timeout on still-waiting entries ──────────────────
        if (entry.Status == "waiting" &&
            _enqueuedAt.TryGetValue(queueId, out var enqueuedAt) &&
            DateTimeOffset.UtcNow - enqueuedAt > _timeout)
        {
            _entries.TryRemove(queueId, out _);
            _enqueuedAt.TryRemove(queueId, out _);

            _logger.LogInformation("Queue entry timed out: QueueId={QueueId}", queueId);

            return Task.FromResult<QueueEntry?>(new QueueEntry(queueId, Status: "timed_out"));
        }

        return Task.FromResult<QueueEntry?>(entry);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns <c>true</c> when the given queue entry has exceeded the configured timeout.
    /// </summary>
    private bool IsExpired(Guid queueId) =>
        _enqueuedAt.TryGetValue(queueId, out var enqueuedAt) &&
        DateTimeOffset.UtcNow - enqueuedAt > _timeout;
}

