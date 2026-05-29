using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ScrambleCoin.Application.Abstractions;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Notifications;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Web.Notifications;

namespace ScrambleCoin.Web.Tests;

/// <summary>
/// Unit tests for <see cref="BroadcastGameEndedOnFinishedHandler"/> (Issue #54).
/// Verifies that game-finished notifications are forwarded to <see cref="IGameBroadcaster"/>
/// with correct data and that broadcast failures are silently swallowed.
/// </summary>
public class BroadcastGameEndedOnFinishedHandlerTests
{
    // ── Factory helpers ───────────────────────────────────────────────────────

    private static (BroadcastGameEndedOnFinishedHandler handler,
                    IGameBroadcaster broadcaster,
                    IGameRepository repo)
        BuildHandler()
    {
        var broadcaster = Substitute.For<IGameBroadcaster>();
        var repo = Substitute.For<IGameRepository>();
        var logger = NullLogger<BroadcastGameEndedOnFinishedHandler>.Instance;
        var handler = new BroadcastGameEndedOnFinishedHandler(broadcaster, repo, logger);
        return (handler, broadcaster, repo);
    }

    /// <summary>Creates a minimal <see cref="Game"/> with two players and a zeroed board.</summary>
    private static Game CreateGame(out Guid playerOneId, out Guid playerTwoId)
    {
        playerOneId = Guid.NewGuid();
        playerTwoId = Guid.NewGuid();
        return new Game(playerOneId, playerTwoId, new Board());
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithWinnerNotification_CallsBroadcastGameEndedAsync()
    {
        // Arrange
        var (handler, broadcaster, repo) = BuildHandler();
        var game = CreateGame(out _, out _);
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var winnerId = Guid.NewGuid();
        var notification = new GameFinished(
            GameId: game.Id,
            WinnerId: winnerId,
            IsDraw: false);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await broadcaster.Received(1).BroadcastGameEndedAsync(
            Arg.Any<Guid>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<Guid?>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCorrectGameId()
    {
        // Arrange
        var (handler, broadcaster, repo) = BuildHandler();
        var game = CreateGame(out _, out _);
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var notification = new GameFinished(
            GameId: game.Id,
            WinnerId: null,
            IsDraw: true);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await broadcaster.Received(1).BroadcastGameEndedAsync(
            game.Id,
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<Guid?>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCorrectWinnerId()
    {
        // Arrange
        var (handler, broadcaster, repo) = BuildHandler();
        var game = CreateGame(out _, out _);
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var winnerId = Guid.NewGuid();
        var notification = new GameFinished(
            GameId: game.Id,
            WinnerId: winnerId,
            IsDraw: false);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await broadcaster.Received(1).BroadcastGameEndedAsync(
            Arg.Any<Guid>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            winnerId,
            false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithDrawNotification_PassesIsDraw_True()
    {
        // Arrange
        var (handler, broadcaster, repo) = BuildHandler();
        var game = CreateGame(out _, out _);
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var notification = new GameFinished(
            GameId: game.Id,
            WinnerId: null,
            IsDraw: true);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await broadcaster.Received(1).BroadcastGameEndedAsync(
            Arg.Any<Guid>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            null,
            true,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LoadsGameFromRepository_UsingNotificationGameId()
    {
        // Arrange
        var (handler, _, repo) = BuildHandler();
        var game = CreateGame(out _, out _);
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var notification = new GameFinished(
            GameId: game.Id,
            WinnerId: null,
            IsDraw: false);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert: repository was queried exactly once with the right game ID
        await repo.Received(1).GetByIdAsync(game.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReadsScoresFromGameForBothPlayers()
    {
        // Arrange — fresh game has zero scores; verify that broadcast is called
        // with playerOneScore=0 and playerTwoScore=0 (mapping from Scores dictionary)
        var (handler, broadcaster, repo) = BuildHandler();
        var game = CreateGame(out _, out _);
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var notification = new GameFinished(
            GameId: game.Id,
            WinnerId: null,
            IsDraw: true);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert: both scores are 0 (fresh game, no coins collected)
        await broadcaster.Received(1).BroadcastGameEndedAsync(
            game.Id,
            0,   // playerOneScore
            0,   // playerTwoScore
            null,
            true,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenBroadcastThrows_ExceptionIsSwallowedAndDoesNotPropagate()
    {
        // Arrange
        var (handler, broadcaster, repo) = BuildHandler();
        var game = CreateGame(out _, out _);
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        broadcaster
            .BroadcastGameEndedAsync(
                Arg.Any<Guid>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<Guid?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("SignalR hub unavailable"));

        var notification = new GameFinished(
            GameId: game.Id,
            WinnerId: null,
            IsDraw: false);

        // Act — must NOT throw
        var exception = await Record.ExceptionAsync(
            () => handler.Handle(notification, CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_ExceptionIsSwallowedAndDoesNotPropagate()
    {
        // Arrange — if the repo itself fails (e.g. game already deleted), handler must not crash
        var (handler, _, repo) = BuildHandler();
        repo
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Game not found"));

        var notification = new GameFinished(
            GameId: Guid.NewGuid(),
            WinnerId: null,
            IsDraw: false);

        // Act — must NOT throw
        var exception = await Record.ExceptionAsync(
            () => handler.Handle(notification, CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }
}
