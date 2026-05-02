using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Application.Interfaces;
using DomainBotReg = ScrambleCoin.Domain.BotRegistrations.BotRegistration;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Factories;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Services;

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

    private sealed record WaitingBot(Guid QueueId, IReadOnlyList<string> LineupPieceNames);

    // ── State ─────────────────────────────────────────────────────────────────

    private readonly ConcurrentQueue<WaitingBot> _waitingQueue = new();
    private readonly ConcurrentDictionary<Guid, QueueEntry> _entries = new();

    // ── DI ────────────────────────────────────────────────────────────────────

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QueueService> _logger;

    public QueueService(IServiceScopeFactory scopeFactory, ILogger<QueueService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // ── IQueueService ─────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<QueueEntry> EnqueueAsync(
        IReadOnlyList<string> lineupPieceNames,
        CancellationToken cancellationToken = default)
    {
        var queueId = Guid.NewGuid();

        // Try to dequeue a waiting bot (compare-and-dequeue is atomic on ConcurrentQueue).
        if (_waitingQueue.TryDequeue(out var waitingBot))
        {
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
        else
        {
            // ── No match yet — park this bot and return 202 ───────────────────
            var entry = new QueueEntry(queueId, Status: "waiting");
            _entries[queueId] = entry;
            _waitingQueue.Enqueue(new WaitingBot(queueId, lineupPieceNames));

            _logger.LogInformation("Bot queued, waiting for opponent: QueueId={QueueId}", queueId);

            return entry;
        }
    }

    /// <inheritdoc />
    public Task<QueueEntry?> PollAsync(Guid queueId, CancellationToken cancellationToken = default)
    {
        _entries.TryGetValue(queueId, out var entry);
        return Task.FromResult(entry);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Lineup BuildLineup(Guid playerId, IReadOnlyList<string> pieceNames)
    {
        var pieces = pieceNames.Select(name => PieceFactory.Create(name, playerId)).ToList();
        return new Lineup(pieces);
    }
}
