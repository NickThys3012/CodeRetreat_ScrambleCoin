using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ScrambleCoin.Application.Abstractions;
using ScrambleCoin.Application.Notifications;

namespace ScrambleCoin.Web.Tests;

/// <summary>
/// Unit tests for <see cref="BroadcastPhaseChangedHandler"/> (Issue #54).
/// Verifies that phase-transition notifications are forwarded to <see cref="IGameBroadcaster"/>
/// and that broadcast failures are silently swallowed.
/// </summary>
public class BroadcastPhaseChangedHandlerTests
{
    // ── Factory helper ────────────────────────────────────────────────────────

    private static (BroadcastPhaseChangedHandler handler, IGameBroadcaster broadcaster)
        BuildHandler()
    {
        var broadcaster = Substitute.For<IGameBroadcaster>();
        var logger = NullLogger<BroadcastPhaseChangedHandler>.Instance;
        var handler = new BroadcastPhaseChangedHandler(broadcaster, logger);
        return (handler, broadcaster);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidNotification_CallsBroadcastPhaseChangedAsync()
    {
        // Arrange
        var (handler, broadcaster) = BuildHandler();
        var gameId = Guid.NewGuid();
        var notification = new TurnPhaseChangedNotification(
            GameId: gameId,
            TurnNumber: 2,
            PreviousPhase: "PlacePhase",
            NewPhase: "MovePhase");

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await broadcaster.Received(1).BroadcastPhaseChangedAsync(
            Arg.Any<Guid>(),
            Arg.Any<int>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCorrectGameId()
    {
        // Arrange
        var (handler, broadcaster) = BuildHandler();
        var gameId = Guid.NewGuid();
        var notification = new TurnPhaseChangedNotification(
            GameId: gameId,
            TurnNumber: 1,
            PreviousPhase: "CoinSpawn",
            NewPhase: "PlacePhase");

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await broadcaster.Received(1).BroadcastPhaseChangedAsync(
            gameId,
            Arg.Any<int>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCorrectTurnNumber()
    {
        // Arrange
        var (handler, broadcaster) = BuildHandler();
        var notification = new TurnPhaseChangedNotification(
            GameId: Guid.NewGuid(),
            TurnNumber: 3,
            PreviousPhase: "MovePhase",
            NewPhase: "CoinSpawn");

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await broadcaster.Received(1).BroadcastPhaseChangedAsync(
            Arg.Any<Guid>(),
            3,
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCorrectPhaseNames()
    {
        // Arrange
        var (handler, broadcaster) = BuildHandler();
        var notification = new TurnPhaseChangedNotification(
            GameId: Guid.NewGuid(),
            TurnNumber: 4,
            PreviousPhase: "PlacePhase",
            NewPhase: "MovePhase");

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await broadcaster.Received(1).BroadcastPhaseChangedAsync(
            Arg.Any<Guid>(),
            Arg.Any<int>(),
            "PlacePhase",
            "MovePhase",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenGameEnding_PassesNullNewPhase()
    {
        // Arrange — NewPhase is null when the game ends after the final MovePhase
        var (handler, broadcaster) = BuildHandler();
        var notification = new TurnPhaseChangedNotification(
            GameId: Guid.NewGuid(),
            TurnNumber: 5,
            PreviousPhase: "MovePhase",
            NewPhase: null);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await broadcaster.Received(1).BroadcastPhaseChangedAsync(
            Arg.Any<Guid>(),
            Arg.Any<int>(),
            "MovePhase",
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenBroadcastThrows_ExceptionIsSwallowedAndDoesNotPropagate()
    {
        // Arrange
        var (handler, broadcaster) = BuildHandler();
        broadcaster
            .BroadcastPhaseChangedAsync(
                Arg.Any<Guid>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("SignalR connection lost"));

        var notification = new TurnPhaseChangedNotification(
            GameId: Guid.NewGuid(),
            TurnNumber: 1,
            PreviousPhase: "CoinSpawn",
            NewPhase: "PlacePhase");

        // Act — must NOT throw
        var exception = await Record.ExceptionAsync(
            () => handler.Handle(notification, CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }
}
