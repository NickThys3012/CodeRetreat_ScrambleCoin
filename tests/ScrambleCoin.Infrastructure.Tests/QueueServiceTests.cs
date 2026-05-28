using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MediatR;
using NSubstitute;
using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Application.Games.Matchmaking;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Infrastructure.Services;

namespace ScrambleCoin.Infrastructure.Tests;

/// <summary>
/// Unit tests for <see cref="QueueService"/> — in-memory matchmaking queue (Issue #37).
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
        BuildQueueService()
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
        var service = new QueueService(scopeFactory, logger);

        return (service, gameRepo, botRegRepo, sender);
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
}
