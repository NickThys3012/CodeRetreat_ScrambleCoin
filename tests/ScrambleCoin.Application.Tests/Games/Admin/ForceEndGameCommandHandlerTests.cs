using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ScrambleCoin.Application.Games.Admin;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Notifications;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Tests.Helpers;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Tests.Games.Admin;

/// <summary>
/// Unit tests for <see cref="ForceEndGameCommandHandler"/> (Issue #57).
/// </summary>
public sealed class ForceEndGameCommandHandlerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static readonly IReadOnlyList<string> DefaultLineup =
        ["Mickey", "Minnie", "Donald", "Goofy", "Scrooge"];

    private static Lineup MakeLineup(Guid playerId) =>
        new(DefaultLineup.Select(n => PieceFactory.Any(n, playerId)).ToList());

    /// <summary>Creates a fresh game in <see cref="GameStatus.WaitingForBots"/> state.</summary>
    private static Game WaitingGame()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        return new Game(p1, p2, new Board());
    }

    /// <summary>Creates a game that has had both lineups set and Start() called — status = InProgress.</summary>
    private static Game InProgressGame()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, MakeLineup(p1));
        game.SetLineup(p2, MakeLineup(p2));
        game.Start();
        return game;
    }

    /// <summary>Creates a game that is already <see cref="GameStatus.Finished"/>.</summary>
    private static Game FinishedGame()
    {
        var game = InProgressGame();
        game.End();
        return game;
    }

    /// <summary>Creates a game that is already <see cref="GameStatus.Cancelled"/>.</summary>
    private static Game CancelledGame()
    {
        var game = WaitingGame();
        game.ForceCancel();
        return game;
    }

    private static ForceEndGameCommandHandler BuildHandler(
        IGameRepository gameRepo,
        IPublisher publisher)
    {
        var logger = Substitute.For<ILogger<ForceEndGameCommandHandler>>();
        return new ForceEndGameCommandHandler(gameRepo, publisher, logger);
    }

    // ── Already-terminal states ───────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenGameIsFinished_SkipsEndingAndDoesNotPublish()
    {
        // Arrange
        var game = FinishedGame();
        var gameRepo  = Substitute.For<IGameRepository>();
        var publisher = Substitute.For<IPublisher>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = BuildHandler(gameRepo, publisher);

        // Act
        await handler.Handle(new ForceEndGameCommand(game.Id), CancellationToken.None);

        // Assert: status stays Finished and no notification is published.
        Assert.Equal(GameStatus.Finished, game.Status);
        await publisher.DidNotReceive().Publish(Arg.Any<GameFinished>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenGameIsCancelled_SkipsEndingAndDoesNotPublish()
    {
        // Arrange
        var game = CancelledGame();
        var gameRepo  = Substitute.For<IGameRepository>();
        var publisher = Substitute.For<IPublisher>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = BuildHandler(gameRepo, publisher);

        // Act
        await handler.Handle(new ForceEndGameCommand(game.Id), CancellationToken.None);

        // Assert: status stays Cancelled and no notification is published.
        Assert.Equal(GameStatus.Cancelled, game.Status);
        await publisher.DidNotReceive().Publish(Arg.Any<GameFinished>(), Arg.Any<CancellationToken>());
    }

    // ── InProgress path ───────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenGameIsInProgress_CallsEndAndPublishesGameFinished()
    {
        // Arrange
        var game = InProgressGame();
        var gameRepo  = Substitute.For<IGameRepository>();
        var publisher = Substitute.For<IPublisher>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = BuildHandler(gameRepo, publisher);

        // Act
        await handler.Handle(new ForceEndGameCommand(game.Id), CancellationToken.None);

        // Assert: game transitions to Finished.
        Assert.Equal(GameStatus.Finished, game.Status);

        // Assert: GameFinished notification is published with the game's ID.
        await publisher.Received(1).Publish(
            Arg.Is<GameFinished>(n => n.GameId == game.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenGameIsInProgress_PublishesCorrectWinnerIdAndIsDraw()
    {
        // Arrange — start with a tied score (0–0) → should be a draw.
        var game = InProgressGame();
        var gameRepo  = Substitute.For<IGameRepository>();
        var publisher = Substitute.For<IPublisher>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        GameFinished? capturedNotification = null;
        await publisher.Publish(
            Arg.Do<GameFinished>(n => capturedNotification = n),
            Arg.Any<CancellationToken>());

        var handler = BuildHandler(gameRepo, publisher);

        // Act
        await handler.Handle(new ForceEndGameCommand(game.Id), CancellationToken.None);

        // Assert: equal scores (both 0) → IsDraw = true, WinnerId = null.
        Assert.NotNull(capturedNotification);
        Assert.True(capturedNotification!.IsDraw);
        Assert.Null(capturedNotification.WinnerId);
    }

    [Fact]
    public async Task Handle_WhenGameIsInProgress_PublishesCorrectWinnerIdAndIsDraw_WhenOnePlayerLeads()
    {
        // Arrange — give PlayerOne a score advantage so the game is not a draw.
        var game = InProgressGame();
        game.AddScore(game.PlayerOne, 5);

        var gameRepo  = Substitute.For<IGameRepository>();
        var publisher = Substitute.For<IPublisher>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        GameFinished? capturedNotification = null;
        await publisher.Publish(
            Arg.Do<GameFinished>(n => capturedNotification = n),
            Arg.Any<CancellationToken>());

        var handler = BuildHandler(gameRepo, publisher);

        // Act
        await handler.Handle(new ForceEndGameCommand(game.Id), CancellationToken.None);

        // Assert: PlayerOne leads → IsDraw = false, WinnerId = PlayerOne.
        Assert.NotNull(capturedNotification);
        Assert.False(capturedNotification!.IsDraw);
        Assert.Equal(game.PlayerOne, capturedNotification.WinnerId);
    }

    [Fact]
    public async Task Handle_WhenGameIsInProgress_SavesGameBeforePublishing()
    {
        // Verify ordering by making SaveAsync throw — Publish must not be called if Save fails.
        var game = InProgressGame();
        var gameRepo  = Substitute.For<IGameRepository>();
        var publisher = Substitute.For<IPublisher>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        gameRepo.SaveAsync(Arg.Any<Game>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Save failed")));

        var handler = BuildHandler(gameRepo, publisher);

        // Act — the handler should propagate the save exception.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new ForceEndGameCommand(game.Id), CancellationToken.None));

        // Assert: if Save raised an exception, Publish was never reached.
        await publisher.DidNotReceive().Publish(
            Arg.Any<GameFinished>(),
            Arg.Any<CancellationToken>());
    }

    // ── WaitingForBots path ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenGameIsWaitingForBots_CallsForceCancelAndDoesNotPublishGameFinished()
    {
        // Arrange
        var game = WaitingGame();
        var gameRepo  = Substitute.For<IGameRepository>();
        var publisher = Substitute.For<IPublisher>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = BuildHandler(gameRepo, publisher);

        // Act
        await handler.Handle(new ForceEndGameCommand(game.Id), CancellationToken.None);

        // Assert: game is cancelled (ForceCancel was called).
        Assert.Equal(GameStatus.Cancelled, game.Status);

        // Assert: no GameFinished notification is published.
        await publisher.DidNotReceive().Publish(
            Arg.Any<GameFinished>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenGameIsWaitingForBots_SavesGame()
    {
        // Arrange
        var game = WaitingGame();
        var gameRepo  = Substitute.For<IGameRepository>();
        var publisher = Substitute.For<IPublisher>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = BuildHandler(gameRepo, publisher);

        // Act
        await handler.Handle(new ForceEndGameCommand(game.Id), CancellationToken.None);

        // Assert: the cancelled game is still persisted.
        await gameRepo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }

    // ── Repository interaction ────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenGameIsFinished_DoesNotCallSaveAsync()
    {
        // Arrange: already-finished game should be skipped entirely.
        var game = FinishedGame();
        var gameRepo  = Substitute.For<IGameRepository>();
        var publisher = Substitute.For<IPublisher>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = BuildHandler(gameRepo, publisher);

        // Act
        await handler.Handle(new ForceEndGameCommand(game.Id), CancellationToken.None);

        // Assert: no persistence call was made.
        await gameRepo.DidNotReceive().SaveAsync(Arg.Any<Game>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenGameIsCancelled_DoesNotCallSaveAsync()
    {
        // Arrange: already-cancelled game should be skipped entirely.
        var game = CancelledGame();
        var gameRepo  = Substitute.For<IGameRepository>();
        var publisher = Substitute.For<IPublisher>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = BuildHandler(gameRepo, publisher);

        // Act
        await handler.Handle(new ForceEndGameCommand(game.Id), CancellationToken.None);

        // Assert: no persistence call was made.
        await gameRepo.DidNotReceive().SaveAsync(Arg.Any<Game>(), Arg.Any<CancellationToken>());
    }
}
