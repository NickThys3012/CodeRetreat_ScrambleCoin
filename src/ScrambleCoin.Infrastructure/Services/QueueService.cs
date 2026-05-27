using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Services;
using DomainBotReg = ScrambleCoin.Domain.BotRegistrations.BotRegistration;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Factories;
using ScrambleCoin.Domain.ValueObjects;

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
    private readonly ILogger<QueueService> _logger;

    /// <param name="scopeFactory">Used to create scoped repository instances.</param>
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

        CleanupExpiredEntries();

        var queueId = Guid.NewGuid();

        // Try to dequeue a waiting bot (compare-and-dequeue is atomic on ConcurrentQueue).
        if (_waitingQueue.TryDequeue(out var waitingBot))
        {
            // Remove the matched waiting bot's token from the tracking set.
            if (waitingBot.BotToken.HasValue)
                _waitingTokens.TryRemove(waitingBot.BotToken.Value, out _);

            _enqueuedAt.TryRemove(waitingBot.QueueId, out _);

            // ── Match found — create game, assign both bots ───────────────────
            using var scope = _scopeFactory.CreateScope();
            var gameRepo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
            var botRegRepo = scope.ServiceProvider.GetRequiredService<IBotRegistrationRepository>();

            var board = new Board();
            var game = Game.CreateShell(board);

            // Assign waiting bot → PlayerOne slot.
            var tokenOne = Guid.NewGuid();
            var lineupOne = BuildLineup(game.PlayerOne, waitingBot.LineupPieceNames);
            game.SetLineup(game.PlayerOne, lineupOne);
            var regOne = new DomainBotReg(tokenOne, game.PlayerOne, game.Id);

            // Assign incoming bot → PlayerTwo slot.
            var tokenTwo = Guid.NewGuid();
            var lineupTwo = BuildLineup(game.PlayerTwo, lineupPieceNames);
            game.SetLineup(game.PlayerTwo, lineupTwo);
            var regTwo = new DomainBotReg(tokenTwo, game.PlayerTwo, game.Id);

            game.Start();

            await gameRepo.SaveAsync(game, cancellationToken);
            await botRegRepo.SaveAsync(regOne, cancellationToken);
            await botRegRepo.SaveAsync(regTwo, cancellationToken);

            // Update the waiting bot's entry so polling returns "matched".
            var matchedOne = new QueueEntry(
                waitingBot.QueueId,
                Status: "matched",
                GameId: game.Id,
                PlayerId: game.PlayerOne,
                Token: tokenOne);
            _entries[waitingBot.QueueId] = matchedOne;

            _logger.LogInformation(
                "Queue matched: GameId={GameId}, P1QueueId={P1}, P2QueueId={P2}",
                game.Id, waitingBot.QueueId, queueId);

            // Return the incoming bot's matched entry (not stored — returned directly).
            return new QueueEntry(
                queueId,
                Status: "matched",
                GameId: game.Id,
                PlayerId: game.PlayerTwo,
                Token: tokenTwo);
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
    /// Removes stale (timed-out) bots from the waiting queue.
    /// Called before pairing to prevent matching a new bot with an already-expired one.
    /// </summary>
    private void CleanupExpiredEntries()
    {
        var now = DateTimeOffset.UtcNow;

        // Drain the queue, filter out expired bots, re-enqueue valid ones.
        var remaining = new List<WaitingBot>();
        while (_waitingQueue.TryDequeue(out var bot))
        {
            if (_enqueuedAt.TryGetValue(bot.QueueId, out var enqueuedAt) && now - enqueuedAt > _timeout)
            {
                // Expired — remove its entry and token tracking.
                _entries.TryRemove(bot.QueueId, out _);
                _enqueuedAt.TryRemove(bot.QueueId, out _);
                if (bot.BotToken.HasValue)
                    _waitingTokens.TryRemove(bot.BotToken.Value, out _);

                _logger.LogInformation(
                    "Expired waiting bot removed during cleanup: QueueId={QueueId}", bot.QueueId);
            }
            else
            {
                remaining.Add(bot);
            }
        }

        foreach (var bot in remaining)
            _waitingQueue.Enqueue(bot);
    }

    private static Lineup BuildLineup(Guid playerId, IReadOnlyList<string> pieceNames)
    {
        var pieces = pieceNames.Select(name => PieceFactory.Create(name, playerId)).ToList();
        return new Lineup(pieces);
    }
}

