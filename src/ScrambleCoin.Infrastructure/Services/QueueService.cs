using System.Collections.Concurrent;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Application.Games.Matchmaking;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Services;
using ScrambleCoin.Domain.Factories;

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

    /// <summary>Reverse index: QueueId → BotToken. Used to clean up <see cref="_waitingTokens"/> on timeout eviction.</summary>
    private readonly ConcurrentDictionary<Guid, Guid> _queueIdToToken = new();

    /// <summary>Timestamp when each queue entry was created, keyed by QueueId.</summary>
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _enqueuedAt = new();

    // ── Configuration ─────────────────────────────────────────────────────────

    private readonly TimeSpan _timeout;

    // ── DI ────────────────────────────────────────────────────────────────────

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QueueService> _logger;

    /// <param name="scopeFactory">
    /// Used to create scoped repository instances for conflict checks and to resolve
    /// <see cref="ISender"/> per-match (avoids captive-dependency when the singleton
    /// captures a scoped MediatR sender).
    /// </param>
    /// <param name="logger">Logger.</param>
    /// <param name="options">
    /// Queue configuration (timeout etc.).  May be <c>null</c> in unit-test contexts;
    /// defaults to 5-minute timeout when absent.
    /// </param>
    public QueueService(
        IServiceScopeFactory scopeFactory,
        ILogger<QueueService> logger,
        IOptions<QueueOptions>? options = null)
    {
        _scopeFactory = scopeFactory;
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
            // Already in an active game?
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

        // ── Validate incoming lineup eagerly — before touching any queue state ─
        // If the incoming lineup is invalid (unknown piece names), throw immediately.
        // This prevents a valid waiting bot from being dequeued and losing its place
        // because the incoming bot supplied a bad request.
        foreach (var pieceName in lineupPieceNames)
        {
            if (PieceFactory.TryCreate(pieceName) is null)
                throw new Domain.Exceptions.DomainException($"Unknown piece name: '{pieceName}'.");
        }

        // ── Clean up timed-out waiting bots before attempting to pair ──────────
        // Uses lazy eviction: skip expired candidates while searching for a match.

        var queueId = Guid.NewGuid();

        // Try to find a non-expired waiting bot.
        WaitingBot? waitingBot = null;
        while (_waitingQueue.TryDequeue(out var candidate))
        {
            // Reject self-match: a bot re-enqueueing with the same token must not be
            // paired with its own waiting entry. However, if PollAsync already evicted
            // the token (timeout), the WaitingBot is an orphan — discard it and continue.
            if (botToken.HasValue && candidate.BotToken == botToken)
            {
                if (_waitingTokens.ContainsKey(botToken.Value))
                {
                    // Token still active — genuine duplicate enqueue, put it back and conflict.
                    _waitingQueue.Enqueue(candidate);
                    _logger.LogWarning(
                        "Conflict: bot token {Token} attempted to match against itself.", botToken.Value);
                    return new QueueEntry(Guid.NewGuid(), Status: "conflict");
                }
                // Token already evicted (PollAsync timeout) — orphaned entry, discard and keep searching.
                _entries.TryRemove(candidate.QueueId, out _);
                _enqueuedAt.TryRemove(candidate.QueueId, out _);
                _queueIdToToken.TryRemove(candidate.QueueId, out _);
                _logger.LogInformation(
                    "Discarded orphaned queue entry after timeout eviction: QueueId={QueueId}", candidate.QueueId);
                continue;
            }

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
            _queueIdToToken.TryRemove(candidate.QueueId, out _);
            _logger.LogInformation("Queue entry expired and evicted: QueueId={QueueId}", candidate.QueueId);
        }

        if (waitingBot is not null)
        {
            // Remove the matched waiting bot's token from the tracking set.
            if (waitingBot.BotToken.HasValue)
            {
                _waitingTokens.TryRemove(waitingBot.BotToken.Value, out _);
                _queueIdToToken.TryRemove(waitingBot.QueueId, out _);
            }

            _enqueuedAt.TryRemove(waitingBot.QueueId, out _);

            // ── Match found — delegate game creation to StartMatchCommand ─────
            // Resolve ISender per-match via a fresh scope to avoid captive-dependency
            // issues (ISender is scoped; QueueService is a singleton).
            using var matchScope = _scopeFactory.CreateScope();
            var sender = matchScope.ServiceProvider.GetRequiredService<ISender>();
            StartMatchResult result;
            try
            {
                result = await sender.Send(
                    new StartMatchCommand(
                        waitingBot.LineupPieceNames, waitingBot.BotToken,
                        lineupPieceNames, botToken),
                    cancellationToken);
            }
            catch
            {
                // Game creation failed — restore Bot 1's entry so it can poll and learn
                // the match attempt failed, rather than being silently stranded as "waiting".
                _entries[waitingBot.QueueId] = new QueueEntry(waitingBot.QueueId, Status: "timed_out");
                _logger.LogError(
                    "StartMatchCommand failed for QueueId={QueueId} — Bot 1 entry marked timed_out.",
                    waitingBot.QueueId);
                throw;
            }

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
        // Check token uniqueness FIRST (atomically), before touching any shared state.
        // This prevents the orphaned-WaitingBot bug: if TryAdd fails, nothing has been
        // written to the queue or dictionaries, so there is nothing to undo.
        if (botToken.HasValue)
        {
            if (!_waitingTokens.TryAdd(botToken.Value, true))
            {
                _logger.LogWarning(
                    "Conflict: bot token {Token} is already waiting in the queue (detected atomically).", botToken.Value);
                return new QueueEntry(Guid.NewGuid(), Status: "conflict");
            }
            // Record the reverse mapping so PollAsync can clean up _waitingTokens on timeout.
            _queueIdToToken[queueId] = botToken.Value;
        }

        var entry = new QueueEntry(queueId, Status: "waiting");
        _entries[queueId] = entry;
        _enqueuedAt[queueId] = DateTimeOffset.UtcNow;
        _waitingQueue.Enqueue(new WaitingBot(queueId, lineupPieceNames, botToken));

        _logger.LogInformation("Bot queued, waiting for opponent: QueueId={QueueId}", queueId);

        return entry;
    }

    /// <inheritdoc />
    public Task<QueueEntry?> PollAsync(Guid queueId, CancellationToken cancellationToken = default)
    {
        if (!_entries.TryGetValue(queueId, out var entry))
            return Task.FromResult<QueueEntry?>(null);

        // ── AC 7: Check for timeout on still-waiting entries ──────────────────
        if (entry.Status != "waiting" ||
            !_enqueuedAt.TryGetValue(queueId, out var enqueuedAt) ||
            DateTimeOffset.UtcNow - enqueuedAt <= _timeout)
        {
            return Task.FromResult<QueueEntry?>(entry);
        }
        _entries.TryRemove(queueId, out _);
        // Do NOT remove _enqueuedAt here. The WaitingBot is still in _waitingQueue and cannot
        // be removed from it directly. Leaving _enqueuedAt intact lets the lazy-eviction loop
        // in EnqueueAsync call IsExpired correctly and discard the orphan instead of matching it.
        if (_queueIdToToken.TryRemove(queueId, out var timedOutToken))
            _waitingTokens.TryRemove(timedOutToken, out _);

        _logger.LogInformation("Queue entry timed out: QueueId={QueueId}", queueId);

        return Task.FromResult<QueueEntry?>(new QueueEntry(queueId, Status: "timed_out"));

    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns <c>true</c> when the given queue entry has exceeded the configured timeout.
    /// </summary>
    private bool IsExpired(Guid queueId) =>
        _enqueuedAt.TryGetValue(queueId, out var enqueuedAt) &&
        DateTimeOffset.UtcNow - enqueuedAt > _timeout;
}

