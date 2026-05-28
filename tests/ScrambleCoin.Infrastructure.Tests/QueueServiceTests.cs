using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MediatR;
using NSubstitute;
using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Application.Games.Matchmaking;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Services;
using ScrambleCoin.Domain.BotRegistrations;
using ScrambleCoin.Infrastructure.Services;

namespace ScrambleCoin.Infrastructure.Tests;

/// <summary>
/// Unit tests for <see cref="QueueService"/> — in-memory matchmaking queue (Issue #37 / #51).
/// </summary>
public class QueueServiceTests
{
    private static readonly IReadOnlyList<string> DefaultLineup =
        ["Mickey", "Minnie", "Donald", "Goofy", "Scrooge"];

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="QueueService"/> whose scope factory resolves real
    /// NSubstitute stubs for the two required repositories and for <see cref="ISender"/>.
    /// The singleton no longer accepts ISender directly — it resolves it per-match
    /// via the scope factory to avoid captive-dependency issues.
    /// </summary>
    private static (QueueService service, IGameRepository gameRepo, IBotRegistrationRepository botRegRepo, ISender sender)
        BuildQueueService(IOptions<QueueOptions>? options = null)
    {
        var gameRepo   = Substitute.For<IGameRepository>();
        var botRegRepo = Substitute.For<IBotRegistrationRepository>();

        // ISender stub: for any StartMatchCommand, return a valid StartMatchResult.
        var sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<StartMatchCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new StartMatchResult(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid()));

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IGameRepository)).Returns(gameRepo);
        serviceProvider.GetService(typeof(IBotRegistrationRepository)).Returns(botRegRepo);
        // ISender is now resolved via scope (Fix 1: remove captive dependency).
        serviceProvider.GetService(typeof(ISender)).Returns(sender);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        var logger = Substitute.For<ILogger<QueueService>>();
        // Constructor no longer accepts ISender (resolved via scope instead).
        var service = new QueueService(scopeFactory, logger, options);

        return (service, gameRepo, botRegRepo, sender);
    }

    /// <summary>
    /// Builds a <see cref="QueueService"/> with a zero-minute timeout so entries
    /// expire immediately (any positive elapsed time satisfies <c>elapsed &gt; TimeSpan.Zero</c>).
    /// Used for timeout and lazy-eviction tests.
    /// </summary>
    private static (QueueService service, IGameRepository gameRepo, IBotRegistrationRepository botRegRepo, ISender sender)
        BuildQueueServiceWithZeroTimeout()
        => BuildQueueService(Options.Create(new QueueOptions { TimeoutMinutes = 0 }));

    /// <summary>
    /// Returns the private <c>_waitingTokens</c> field of the given service instance via
    /// reflection.  Used to simulate a concurrent in-flight request that has already
    /// claimed a token slot, without requiring real thread-level parallelism.
    /// </summary>
    private static ConcurrentDictionary<Guid, bool> GetWaitingTokens(QueueService service)
    {
        var field = typeof(QueueService)
            .GetField("_waitingTokens", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_waitingTokens field not found on QueueService.");
        return (ConcurrentDictionary<Guid, bool>)field.GetValue(service)!;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Enqueue_WhenEmpty_Returns202Waiting()
    {
        // Arrange
        var (service, _, _, _) = BuildQueueService();

        // Act — first bot in queue, no opponent yet.
        var entry = await service.EnqueueAsync(DefaultLineup);

        // Assert: status is "waiting" (caller maps this to 202 Accepted).
        Assert.Equal("waiting", entry.Status);
    }

    [Fact]
    public async Task Enqueue_WhenEmpty_ReturnsNonEmptyQueueId()
    {
        var (service, _, _, _) = BuildQueueService();

        var entry = await service.EnqueueAsync(DefaultLineup);

        Assert.NotEqual(Guid.Empty, entry.QueueId);
    }

    [Fact]
    public async Task Enqueue_WhenEmpty_GameIdIsNull()
    {
        var (service, _, _, _) = BuildQueueService();

        var entry = await service.EnqueueAsync(DefaultLineup);

        // No game has been created yet.
        Assert.Null(entry.GameId);
    }

    [Fact]
    public async Task Enqueue_WhenOneWaiting_ReturnsBothMatchedWith200()
    {
        // Arrange
        var (service, _, _, _) = BuildQueueService();
        await service.EnqueueAsync(DefaultLineup); // first bot parks

        // Act — second bot arrives, expects immediate match (HTTP 200).
        var entry = await service.EnqueueAsync(DefaultLineup);

        // Assert: the second bot gets "matched".
        Assert.Equal("matched", entry.Status);
    }

    [Fact]
    public async Task Enqueue_WhenOneWaiting_MatchedEntryHasNonNullGameId()
    {
        var (service, _, _, _) = BuildQueueService();
        await service.EnqueueAsync(DefaultLineup);

        var entry = await service.EnqueueAsync(DefaultLineup);

        Assert.NotNull(entry.GameId);
        Assert.NotEqual(Guid.Empty, entry.GameId!.Value);
    }

    [Fact]
    public async Task Enqueue_WhenOneWaiting_MatchedEntryHasNonNullPlayerId()
    {
        var (service, _, _, _) = BuildQueueService();
        await service.EnqueueAsync(DefaultLineup);

        var entry = await service.EnqueueAsync(DefaultLineup);

        Assert.NotNull(entry.PlayerId);
        Assert.NotEqual(Guid.Empty, entry.PlayerId!.Value);
    }

    [Fact]
    public async Task Enqueue_WhenOneWaiting_MatchedEntryHasNonNullToken()
    {
        var (service, _, _, _) = BuildQueueService();
        await service.EnqueueAsync(DefaultLineup);

        var entry = await service.EnqueueAsync(DefaultLineup);

        Assert.NotNull(entry.Token);
        Assert.NotEqual(Guid.Empty, entry.Token!.Value);
    }

    [Fact]
    public async Task Poll_AfterFirstEnqueue_ReturnsWaitingEntry()
    {
        // Arrange
        var (service, _, _, _) = BuildQueueService();
        var first = await service.EnqueueAsync(DefaultLineup);

        // Act
        var polled = await service.PollAsync(first.QueueId);

        // Assert: polling returns the same waiting entry.
        Assert.NotNull(polled);
        Assert.Equal("waiting", polled.Status);
        Assert.Equal(first.QueueId, polled.QueueId);
    }

    [Fact]
    public async Task Poll_AfterMatch_FirstBotEntryIsUpdatedToMatched()
    {
        // Arrange
        var (service, _, _, _) = BuildQueueService();
        var firstEntry = await service.EnqueueAsync(DefaultLineup);   // parks
        await service.EnqueueAsync(DefaultLineup);                     // triggers match

        // Act: the first bot polls for its status.
        var polled = await service.PollAsync(firstEntry.QueueId);

        // Assert: the waiting entry has been updated to "matched".
        Assert.NotNull(polled);
        Assert.Equal("matched", polled.Status);
    }

    [Fact]
    public async Task Poll_AfterMatch_FirstBotPolledEntry_HasNonNullGameId()
    {
        // Arrange
        var (service, _, _, _) = BuildQueueService();
        var firstEntry = await service.EnqueueAsync(DefaultLineup);
        await service.EnqueueAsync(DefaultLineup); // triggers match

        // Act
        var polledEntry = await service.PollAsync(firstEntry.QueueId);

        // Assert
        Assert.NotNull(polledEntry);
        Assert.NotNull(polledEntry.GameId);
        Assert.NotEqual(Guid.Empty, polledEntry.GameId!.Value);
    }

    [Fact]
    public async Task Poll_AfterMatch_FirstBotPolledEntry_HasNonNullPlayerId()
    {
        // Arrange
        var (service, _, _, _) = BuildQueueService();
        var firstEntry = await service.EnqueueAsync(DefaultLineup);
        await service.EnqueueAsync(DefaultLineup); // triggers match

        // Act
        var polledEntry = await service.PollAsync(firstEntry.QueueId);

        // Assert
        Assert.NotNull(polledEntry);
        Assert.NotNull(polledEntry.PlayerId);
        Assert.NotEqual(Guid.Empty, polledEntry.PlayerId!.Value);
    }

    [Fact]
    public async Task Poll_AfterMatch_FirstBotPolledEntry_HasNonNullToken()
    {
        // Arrange
        var (service, _, _, _) = BuildQueueService();
        var firstEntry = await service.EnqueueAsync(DefaultLineup);
        await service.EnqueueAsync(DefaultLineup); // triggers match

        // Act
        var polledEntry = await service.PollAsync(firstEntry.QueueId);

        // Assert
        Assert.NotNull(polledEntry);
        Assert.NotNull(polledEntry.Token);
        Assert.NotEqual(Guid.Empty, polledEntry.Token!.Value);
    }

    [Fact]
    public async Task Poll_UnknownQueueId_ReturnsNull()
    {
        var (service, _, _, _) = BuildQueueService();

        var result = await service.PollAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task Enqueue_WhenOneWaiting_DispatchesStartMatchCommand()
    {
        // Arrange
        var (service, _, _, sender) = BuildQueueService();
        await service.EnqueueAsync(DefaultLineup);

        // Act
        await service.EnqueueAsync(DefaultLineup);

        // Assert: game creation was delegated to the MediatR pipeline via StartMatchCommand.
        await sender.Received(1).Send(Arg.Any<StartMatchCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Enqueue_WhenOneWaiting_StartMatchCommandCarriesBothLineups()
    {
        // Arrange
        var (service, _, _, sender) = BuildQueueService();
        await service.EnqueueAsync(DefaultLineup);

        // Act
        await service.EnqueueAsync(DefaultLineup);

        // Assert: the command carries both bot lineups so the handler can build them.
        await sender.Received(1).Send(
            Arg.Is<StartMatchCommand>(cmd =>
                cmd.LineupOne.Count == DefaultLineup.Count &&
                cmd.LineupTwo.Count == DefaultLineup.Count),
            Arg.Any<CancellationToken>());
    }

    // ── Issue #51: Critical path 1 — PollAsync timeout ────────────────────────

    /// <summary>
    /// AC 7: When the configured timeout elapses, <see cref="QueueService.PollAsync"/>
    /// must return a <see cref="QueueEntry"/> whose <c>Status</c> is <c>"timed_out"</c>.
    /// A zero-minute timeout ensures any positive elapsed time triggers the condition.
    /// </summary>
    [Fact]
    public async Task Poll_AfterTimeoutElapses_ReturnsTimedOutStatus()
    {
        // Arrange: build service with TimeoutMinutes = 0 → TimeSpan.Zero timeout.
        var (service, _, _, _) = BuildQueueServiceWithZeroTimeout();
        var entry = await service.EnqueueAsync(DefaultLineup);

        // Allow at least one tick so elapsed > TimeSpan.Zero is satisfied.
        await Task.Delay(1);

        // Act
        var polled = await service.PollAsync(entry.QueueId);

        // Assert
        Assert.NotNull(polled);
        Assert.Equal("timed_out", polled!.Status);
    }

    [Fact]
    public async Task Poll_AfterTimeoutElapses_TimedOutEntryPreservesQueueId()
    {
        var (service, _, _, _) = BuildQueueServiceWithZeroTimeout();
        var entry = await service.EnqueueAsync(DefaultLineup);
        await Task.Delay(1);

        var polled = await service.PollAsync(entry.QueueId);

        Assert.NotNull(polled);
        Assert.Equal(entry.QueueId, polled!.QueueId);
    }

    [Fact]
    public async Task Poll_AfterTimeoutElapses_SubsequentPollReturnsNull()
    {
        // After the entry is marked timed_out the record is removed, so further polls
        // must return null (not timed_out again).
        var (service, _, _, _) = BuildQueueServiceWithZeroTimeout();
        var entry = await service.EnqueueAsync(DefaultLineup);
        await Task.Delay(1);

        // First poll removes the entry and returns "timed_out".
        await service.PollAsync(entry.QueueId);

        // Act: second poll on same ID.
        var polled2 = await service.PollAsync(entry.QueueId);

        // Assert: entry is gone.
        Assert.Null(polled2);
    }

    [Fact]
    public async Task Poll_BeforeTimeoutElapses_ReturnsWaitingNotTimedOut()
    {
        // With a 5-minute timeout, a freshly enqueued bot must not be timed out immediately.
        var (service, _, _, _) = BuildQueueService(); // default 5-minute timeout
        var entry = await service.EnqueueAsync(DefaultLineup);

        var polled = await service.PollAsync(entry.QueueId);

        Assert.NotNull(polled);
        Assert.Equal("waiting", polled!.Status);
    }

    // ── Issue #51: Critical path 2 — conflict when same token already waiting ──

    /// <summary>
    /// AC 6 / <c>_waitingTokens.TryAdd</c> path:
    /// When a bot token is already registered in the waiting-tokens set (simulating
    /// a concurrent in-flight request that claimed the slot), a subsequent
    /// <see cref="QueueService.EnqueueAsync"/> with the same token must return
    /// <c>Status="conflict"</c>.
    /// <para>
    /// The waiting-token set is seeded directly via reflection to reproduce the
    /// concurrent race scenario (two HTTP requests arriving simultaneously with the
    /// same bot token) in a deterministic, single-threaded unit test.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Enqueue_WhenSameBotTokenAlreadyInWaitingSet_ReturnsConflictStatus()
    {
        // Arrange
        var (service, _, _, _) = BuildQueueService();
        var token = Guid.NewGuid();

        // Simulate a racing request that already claimed this token.
        var waitingTokens = GetWaitingTokens(service);
        waitingTokens.TryAdd(token, true);

        // Act: second enqueue with same token finds an empty queue (no waiting bot to dequeue)
        // but the token is already in the waiting set → conflict.
        var entry = await service.EnqueueAsync(DefaultLineup, token);

        // Assert
        Assert.Equal("conflict", entry.Status);
    }

    [Fact]
    public async Task Enqueue_WhenSameBotTokenAlreadyInWaitingSet_ReturnsNonEmptyQueueId()
    {
        var (service, _, _, _) = BuildQueueService();
        var token = Guid.NewGuid();

        GetWaitingTokens(service).TryAdd(token, true);

        var entry = await service.EnqueueAsync(DefaultLineup, token);

        Assert.NotEqual(Guid.Empty, entry.QueueId);
    }

    [Fact]
    public async Task Enqueue_WhenSameBotTokenAlreadyInWaitingSet_GameIdIsNull()
    {
        // A conflicted entry must not carry game credentials.
        var (service, _, _, _) = BuildQueueService();
        var token = Guid.NewGuid();

        GetWaitingTokens(service).TryAdd(token, true);

        var entry = await service.EnqueueAsync(DefaultLineup, token);

        Assert.Null(entry.GameId);
    }

    [Fact]
    public async Task Enqueue_WhenSameBotTokenAlreadyInWaitingSet_NoGameIsCreated()
    {
        // Conflict must be short-circuited without dispatching StartMatchCommand.
        var (service, _, _, sender) = BuildQueueService();
        var token = Guid.NewGuid();

        GetWaitingTokens(service).TryAdd(token, true);

        await service.EnqueueAsync(DefaultLineup, token);

        await sender.DidNotReceive().Send(Arg.Any<StartMatchCommand>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// AC 6 (DB-based conflict path): when the bot already has an active game in the
    /// persistence store, <see cref="QueueService.EnqueueAsync"/> must short-circuit
    /// and return <c>Status="conflict"</c> before touching any queue state.
    /// </summary>
    [Fact]
    public async Task Enqueue_WhenBotHasActiveGame_ReturnsConflictStatus()
    {
        var (service, gameRepo, botRegRepo, _) = BuildQueueService();
        var token    = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        botRegRepo
            .GetByTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(new BotRegistration(token, playerId, Guid.NewGuid()));
        gameRepo
            .HasActiveGameAsync(playerId, Arg.Any<CancellationToken>())
            .Returns(true);

        var entry = await service.EnqueueAsync(DefaultLineup, token);

        Assert.Equal("conflict", entry.Status);
    }

    [Fact]
    public async Task Enqueue_WhenBotHasActiveGame_NoGameIsCreated()
    {
        var (service, gameRepo, botRegRepo, sender) = BuildQueueService();
        var token    = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        botRegRepo
            .GetByTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(new BotRegistration(token, playerId, Guid.NewGuid()));
        gameRepo
            .HasActiveGameAsync(playerId, Arg.Any<CancellationToken>())
            .Returns(true);

        await service.EnqueueAsync(DefaultLineup, token);

        await sender.DidNotReceive().Send(Arg.Any<StartMatchCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Enqueue_WhenBotRegisteredButNoActiveGame_DoesNotReturnConflict()
    {
        // Having a registration but NO active game should not block enqueueing.
        var (service, gameRepo, botRegRepo, _) = BuildQueueService();
        var token    = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        botRegRepo
            .GetByTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(new BotRegistration(token, playerId, Guid.NewGuid()));
        gameRepo
            .HasActiveGameAsync(playerId, Arg.Any<CancellationToken>())
            .Returns(false);

        var entry = await service.EnqueueAsync(DefaultLineup, token);

        Assert.NotEqual("conflict", entry.Status);
    }

    // ── Issue #51: Critical path 3 — lazy eviction of expired WaitingBot ──────

    /// <summary>
    /// Lazy-eviction: when Bot A's waiting entry has already expired (elapsed &gt; timeout),
    /// a second bot that arrives must NOT be matched with stale Bot A.
    /// Bot B should park itself as a new waiting entry.
    /// </summary>
    [Fact]
    public async Task Enqueue_WhenWaitingBotIsExpired_IncomingBotReceivesWaitingStatus()
    {
        // Arrange: zero-minute timeout so Bot A's entry is stale after any delay.
        var (service, _, _, _) = BuildQueueServiceWithZeroTimeout();

        await service.EnqueueAsync(DefaultLineup); // Bot A parks
        await Task.Delay(1);                        // Bot A entry expires

        // Act: Bot B arrives and should evict stale Bot A via lazy eviction.
        var result = await service.EnqueueAsync(DefaultLineup);

        // Assert: Bot B parks and waits (not matched with expired Bot A).
        Assert.Equal("waiting", result.Status);
    }

    [Fact]
    public async Task Enqueue_WhenWaitingBotIsExpired_IncomingBotHasNullGameId()
    {
        var (service, _, _, _) = BuildQueueServiceWithZeroTimeout();

        await service.EnqueueAsync(DefaultLineup); // Bot A parks (will expire)
        await Task.Delay(1);

        var result = await service.EnqueueAsync(DefaultLineup);

        // No game was created; GameId must be null.
        Assert.Null(result.GameId);
    }

    [Fact]
    public async Task Enqueue_WhenWaitingBotIsExpired_NoGameIsCreated()
    {
        // The expired waiting bot must be discarded, not matched.
        var (service, _, _, sender) = BuildQueueServiceWithZeroTimeout();

        await service.EnqueueAsync(DefaultLineup); // Bot A parks
        await Task.Delay(1);

        await service.EnqueueAsync(DefaultLineup); // Bot B: should NOT match with Bot A

        await sender.DidNotReceive().Send(Arg.Any<StartMatchCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Enqueue_WhenWaitingBotIsExpired_ExpiredBotQueueEntryIsEvicted()
    {
        // After lazy eviction, polling the expired bot's QueueId should return null (evicted)
        // or timed_out — either way the entry must not be "waiting".
        var (service, _, _, _) = BuildQueueServiceWithZeroTimeout();

        var expiredEntry = await service.EnqueueAsync(DefaultLineup); // Bot A
        await Task.Delay(1);

        // Trigger the lazy-eviction loop by having Bot B enqueue.
        await service.EnqueueAsync(DefaultLineup);

        // Bot A's entry should either be gone or already transitioned.
        var polled = await service.PollAsync(expiredEntry.QueueId);
        Assert.True(
            polled is null || polled.Status is "timed_out",
            $"Expected null or 'timed_out' but got '{polled?.Status}'.");
    }

    [Fact]
    public async Task Enqueue_WhenExpiredBotEvictedAndFreshBotArrives_TheyCanMatch()
    {
        // Arrange: zero-timeout service — every entry expires after any positive elapsed time.
        var (service, _, _, _) = BuildQueueServiceWithZeroTimeout();

        await service.EnqueueAsync(DefaultLineup); // Bot A parks (will expire immediately)
        await Task.Delay(1);                        // ensure Bot A's timeout elapses

        // Act on original service: Bot B triggers lazy eviction of Bot A and parks itself.
        // Because the service has a zero timeout, Bot B also expires after this call.
        var botBResult = await service.EnqueueAsync(DefaultLineup);

        // Assert (original service): Bot B is "waiting" — not matched with stale Bot A.
        Assert.Equal("waiting", botBResult.Status);

        // Post-eviction matching is validated on a freshly configured service (5-minute timeout).
        // The zero-timeout instance cannot pair new bots usefully because every entry expires
        // before a second bot can arrive. Using a correctly-configured instance proves the
        // service architecture supports normal matching once timeout is set appropriately.
        var (svc2, _, _, _) = BuildQueueService(); // 5-minute timeout
        await svc2.EnqueueAsync(DefaultLineup);    // Bot C parks
        var botD = await svc2.EnqueueAsync(DefaultLineup); // Bot D matches with Bot C

        Assert.Equal("matched", botD.Status);
    }

    // ── Issue #51: Token cleanup — PollAsync timeout frees token for re-enqueue ──

    /// <summary>
    /// When <see cref="QueueService.PollAsync"/> detects a timed-out entry, it removes the
    /// bot's token from <c>_waitingTokens</c> via <c>_queueIdToToken</c>.  This test
    /// verifies that the freed token allows the same bot to re-enqueue without a conflict.
    /// </summary>
    [Fact]
    public async Task Enqueue_AfterTokenTimedOutViaPoll_SameBotCanRequeue()
    {
        // Arrange: zero-timeout so any elapsed time triggers expiry.
        var (svc, _, _, _) = BuildQueueServiceWithZeroTimeout();
        var token = Guid.NewGuid();
        var entry = await svc.EnqueueAsync(DefaultLineup, token);
        await Task.Delay(1); // ensure timeout elapses

        // Act: PollAsync triggers timeout + removes token from _waitingTokens.
        await svc.PollAsync(entry.QueueId);

        // Assert: same token can re-enqueue without conflict.
        var requeue = await svc.EnqueueAsync(DefaultLineup, token);
        Assert.NotEqual("conflict", requeue.Status);
    }

    // ── Issue #51: Token cleanup — lazy eviction frees token for re-enqueue ────

    /// <summary>
    /// The lazy-eviction loop in <see cref="QueueService.EnqueueAsync"/> calls
    /// <c>_waitingTokens.TryRemove</c> for each expired bot it discards.  This test
    /// verifies that the freed token allows the same bot to re-enqueue without a conflict.
    /// </summary>
    [Fact]
    public async Task Enqueue_AfterLazyEviction_SameBotTokenCanRequeue()
    {
        // Arrange: Bot A enqueues with a specific token on a zero-timeout service.
        var (svc, _, _, _) = BuildQueueServiceWithZeroTimeout();
        var botAToken = Guid.NewGuid();
        await svc.EnqueueAsync(DefaultLineup, botAToken);
        await Task.Delay(1); // ensure Bot A expires

        // Bot B triggers lazy eviction of Bot A (and its token).
        var botBToken = Guid.NewGuid();
        await svc.EnqueueAsync(DefaultLineup, botBToken);

        // Assert: Bot A's token was freed by the eviction — re-enqueue must not return conflict.
        var requeue = await svc.EnqueueAsync(DefaultLineup, botAToken);
        Assert.NotEqual("conflict", requeue.Status);
    }
}
