using Microsoft.Extensions.Logging;
using NSubstitute;
using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Application.Games.GetGameResult;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.ValueObjects;
using DomainBotReg = ScrambleCoin.Domain.BotRegistrations.BotRegistration;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Unit tests for <see cref="GetGameResultQueryHandler"/> (Issue #51).
/// Verifies authorization, game-status guards, and winner/draw calculation.
/// </summary>
public class GetGameResultQueryHandlerTests
{
    private static readonly IReadOnlyList<string> DefaultLineup =
        ["Mickey", "Minnie", "Donald", "Goofy", "Scrooge"];

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GetGameResultQueryHandler BuildHandler(
        IGameRepository gameRepo,
        IBotRegistrationRepository botRegRepo)
    {
        var logger = Substitute.For<ILogger<GetGameResultQueryHandler>>();
        return new GetGameResultQueryHandler(gameRepo, botRegRepo, logger);
    }

    /// <summary>
    /// Creates a <see cref="Game"/> in <see cref="GameStatus.Finished"/> state with the
    /// supplied per-player scores applied before ending the game.
    /// </summary>
    private static Game BuildFinishedGame(int scoreP1, int scoreP2)
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var pieces1 = DefaultLineup
            .Select(n => Domain.Factories.PieceFactory.Create(n, p1))
            .ToList();
        var pieces2 = DefaultLineup
            .Select(n => Domain.Factories.PieceFactory.Create(n, p2))
            .ToList();

        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(pieces1));
        game.SetLineup(p2, new Lineup(pieces2));
        game.Start(); // → InProgress

        if (scoreP1 > 0) game.AddScore(p1, scoreP1);
        if (scoreP2 > 0) game.AddScore(p2, scoreP2);

        game.End(); // → Finished
        return game;
    }

    // ── Authorization tests ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTokenNotFound_ThrowsUnauthorizedGameAccessException()
    {
        // Arrange: token is unknown — repository returns null.
        var gameId = Guid.NewGuid();
        var unknownToken = Guid.NewGuid();

        var botRegRepo = Substitute.For<IBotRegistrationRepository>();
        botRegRepo.GetByTokenAsync(unknownToken, Arg.Any<CancellationToken>())
            .Returns((DomainBotReg?)null);

        var gameRepo = Substitute.For<IGameRepository>();
        var handler = BuildHandler(gameRepo, botRegRepo);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedGameAccessException>(() =>
            handler.Handle(new GetGameResultQuery(gameId, unknownToken), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenTokenBelongsToDifferentGame_ThrowsUnauthorizedGameAccessException()
    {
        // Arrange: token is valid but belongs to a different gameId.
        var requestedGameId = Guid.NewGuid();
        var differentGameId = Guid.NewGuid();
        var token = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        var botRegRepo = Substitute.For<IBotRegistrationRepository>();
        botRegRepo.GetByTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(new DomainBotReg(token, playerId, differentGameId));

        var gameRepo = Substitute.For<IGameRepository>();
        var handler = BuildHandler(gameRepo, botRegRepo);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedGameAccessException>(() =>
            handler.Handle(new GetGameResultQuery(requestedGameId, token), CancellationToken.None));
    }

    // ── Game-status guard ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenGameIsNotFinished_ThrowsGameNotFinishedException()
    {
        // Arrange: game is still InProgress.
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var token = Guid.NewGuid();

        var pieces1 = DefaultLineup.Select(n => Domain.Factories.PieceFactory.Create(n, p1)).ToList();
        var pieces2 = DefaultLineup.Select(n => Domain.Factories.PieceFactory.Create(n, p2)).ToList();

        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(pieces1));
        game.SetLineup(p2, new Lineup(pieces2));
        game.Start(); // Status == InProgress

        var botRegRepo = Substitute.For<IBotRegistrationRepository>();
        botRegRepo.GetByTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(new DomainBotReg(token, p1, game.Id));

        var gameRepo = Substitute.For<IGameRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = BuildHandler(gameRepo, botRegRepo);

        // Act & Assert
        await Assert.ThrowsAsync<GameNotFinishedException>(() =>
            handler.Handle(new GetGameResultQuery(game.Id, token), CancellationToken.None));
    }

    // ── Winner calculation ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenPlayerOneScoreHigher_ReturnsPlayerOneAsWinner()
    {
        // Arrange: P1 = 10 points, P2 = 5 points.
        var game = BuildFinishedGame(scoreP1: 10, scoreP2: 5);
        var token = Guid.NewGuid();

        var botRegRepo = Substitute.For<IBotRegistrationRepository>();
        botRegRepo.GetByTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(new DomainBotReg(token, game.PlayerOne, game.Id));

        var gameRepo = Substitute.For<IGameRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = BuildHandler(gameRepo, botRegRepo);

        // Act
        var result = await handler.Handle(new GetGameResultQuery(game.Id, token), CancellationToken.None);

        // Assert
        Assert.Equal(game.PlayerOne, result.WinnerId);
    }

    [Fact]
    public async Task Handle_WhenPlayerOneScoreHigher_IsDrawIsFalse()
    {
        // Arrange
        var game = BuildFinishedGame(scoreP1: 10, scoreP2: 5);
        var token = Guid.NewGuid();

        var botRegRepo = Substitute.For<IBotRegistrationRepository>();
        botRegRepo.GetByTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(new DomainBotReg(token, game.PlayerOne, game.Id));

        var gameRepo = Substitute.For<IGameRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = BuildHandler(gameRepo, botRegRepo);

        // Act
        var result = await handler.Handle(new GetGameResultQuery(game.Id, token), CancellationToken.None);

        // Assert
        Assert.False(result.IsDraw);
    }

    [Fact]
    public async Task Handle_WhenPlayerTwoScoreHigher_ReturnsPlayerTwoAsWinner()
    {
        // Arrange: P2 = 7 points, P1 = 3 points.
        var game = BuildFinishedGame(scoreP1: 3, scoreP2: 7);
        var token = Guid.NewGuid();

        var botRegRepo = Substitute.For<IBotRegistrationRepository>();
        botRegRepo.GetByTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(new DomainBotReg(token, game.PlayerTwo, game.Id));

        var gameRepo = Substitute.For<IGameRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = BuildHandler(gameRepo, botRegRepo);

        // Act
        var result = await handler.Handle(new GetGameResultQuery(game.Id, token), CancellationToken.None);

        // Assert
        Assert.Equal(game.PlayerTwo, result.WinnerId);
    }

    [Fact]
    public async Task Handle_WhenPlayerTwoScoreHigher_IsDrawIsFalse()
    {
        // Arrange
        var game = BuildFinishedGame(scoreP1: 3, scoreP2: 7);
        var token = Guid.NewGuid();

        var botRegRepo = Substitute.For<IBotRegistrationRepository>();
        botRegRepo.GetByTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(new DomainBotReg(token, game.PlayerTwo, game.Id));

        var gameRepo = Substitute.For<IGameRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = BuildHandler(gameRepo, botRegRepo);

        // Act
        var result = await handler.Handle(new GetGameResultQuery(game.Id, token), CancellationToken.None);

        // Assert
        Assert.False(result.IsDraw);
    }

    [Fact]
    public async Task Handle_WhenScoresAreEqual_IsDraw()
    {
        // Arrange: both players score 5 — draw.
        var game = BuildFinishedGame(scoreP1: 5, scoreP2: 5);
        var token = Guid.NewGuid();

        var botRegRepo = Substitute.For<IBotRegistrationRepository>();
        botRegRepo.GetByTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(new DomainBotReg(token, game.PlayerOne, game.Id));

        var gameRepo = Substitute.For<IGameRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = BuildHandler(gameRepo, botRegRepo);

        // Act
        var result = await handler.Handle(new GetGameResultQuery(game.Id, token), CancellationToken.None);

        // Assert
        Assert.True(result.IsDraw);
    }

    [Fact]
    public async Task Handle_WhenScoresAreEqual_WinnerIdIsNull()
    {
        // Arrange: both players score 5 — draw.
        var game = BuildFinishedGame(scoreP1: 5, scoreP2: 5);
        var token = Guid.NewGuid();

        var botRegRepo = Substitute.For<IBotRegistrationRepository>();
        botRegRepo.GetByTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(new DomainBotReg(token, game.PlayerOne, game.Id));

        var gameRepo = Substitute.For<IGameRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = BuildHandler(gameRepo, botRegRepo);

        // Act
        var result = await handler.Handle(new GetGameResultQuery(game.Id, token), CancellationToken.None);

        // Assert
        Assert.Null(result.WinnerId);
    }

    // ── DTO field correctness ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenGameFinished_ReturnsDtoWithCorrectGameId()
    {
        // Arrange
        var game = BuildFinishedGame(scoreP1: 3, scoreP2: 1);
        var token = Guid.NewGuid();

        var botRegRepo = Substitute.For<IBotRegistrationRepository>();
        botRegRepo.GetByTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(new DomainBotReg(token, game.PlayerOne, game.Id));

        var gameRepo = Substitute.For<IGameRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = BuildHandler(gameRepo, botRegRepo);

        // Act
        var result = await handler.Handle(new GetGameResultQuery(game.Id, token), CancellationToken.None);

        // Assert
        Assert.Equal(game.Id, result.GameId);
    }

    [Fact]
    public async Task Handle_WhenGameFinished_StatusFieldIsFinished()
    {
        // Arrange
        var game = BuildFinishedGame(scoreP1: 0, scoreP2: 0);
        var token = Guid.NewGuid();

        var botRegRepo = Substitute.For<IBotRegistrationRepository>();
        botRegRepo.GetByTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(new DomainBotReg(token, game.PlayerOne, game.Id));

        var gameRepo = Substitute.For<IGameRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = BuildHandler(gameRepo, botRegRepo);

        // Act
        var result = await handler.Handle(new GetGameResultQuery(game.Id, token), CancellationToken.None);

        // Assert
        Assert.Equal("finished", result.Status);
    }
}
