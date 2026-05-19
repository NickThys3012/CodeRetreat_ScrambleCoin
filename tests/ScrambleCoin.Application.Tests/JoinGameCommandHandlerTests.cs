using Microsoft.Extensions.Logging;
using NSubstitute;
using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Application.Games.JoinGame;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Unit tests for <see cref="JoinGameCommandHandler"/> (Issue #37).
/// </summary>
public class JoinGameCommandHandlerTests
{
    private static readonly IReadOnlyList<string> DefaultLineup =
        ["Mickey", "Minnie", "Donald", "Goofy", "Scrooge"];

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Creates a fresh game shell in WaitingForBots state.</summary>
    private static Game NewShell() => Game.CreateShell(new Board());

    private static JoinGameCommandHandler BuildHandler(
        IGameRepository gameRepo,
        IBotRegistrationRepository? botRegRepo = null)
    {
        var logger = Substitute.For<ILogger<JoinGameCommandHandler>>();
        botRegRepo ??= Substitute.For<IBotRegistrationRepository>();
        return new JoinGameCommandHandler(gameRepo, botRegRepo, logger);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_FirstPlayer_AssignsPlayerOneSlot()
    {
        // Arrange
        var game = NewShell();
        var gameRepo = Substitute.For<IGameRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = BuildHandler(gameRepo);
        var command = new JoinGameCommand(game.Id, DefaultLineup);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: the assigned player ID is PlayerOne.
        Assert.Equal(game.PlayerOne, result.PlayerId);
    }

    [Fact]
    public async Task Handle_FirstPlayer_ReturnsNonEmptyToken()
    {
        // Arrange
        var game = NewShell();
        var gameRepo = Substitute.For<IGameRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = BuildHandler(gameRepo);
        var command = new JoinGameCommand(game.Id, DefaultLineup);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: a non-empty bearer token is issued.
        Assert.NotEqual(Guid.Empty, result.Token);
    }

    [Fact]
    public async Task Handle_FirstPlayer_SetsLineupOnGame()
    {
        // Arrange
        var game = NewShell();
        var gameRepo = Substitute.For<IGameRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = BuildHandler(gameRepo);
        var command = new JoinGameCommand(game.Id, DefaultLineup);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: LineupPlayerOne is now set; game is still WaitingForBots (only 1 joined).
        Assert.NotNull(game.LineupPlayerOne);
        Assert.Equal(GameStatus.WaitingForBots, game.Status);
    }

    [Fact]
    public async Task Handle_SecondPlayer_AssignsPlayerTwoSlot()
    {
        // Arrange
        var game = NewShell();

        // First bot joins.
        game.SetLineup(game.PlayerOne,
            new Domain.ValueObjects.Lineup(
                DefaultLineup.Select(n => Domain.Factories.PieceFactory.Create(n, game.PlayerOne)).ToList()));

        var gameRepo = Substitute.For<IGameRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = BuildHandler(gameRepo);
        var command = new JoinGameCommand(game.Id, DefaultLineup);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: the assigned player ID is PlayerTwo.
        Assert.Equal(game.PlayerTwo, result.PlayerId);
    }

    [Fact]
    public async Task Handle_SecondPlayer_StartsGame()
    {
        // Arrange
        var game = NewShell();

        // First bot joins (sets PlayerOne lineup).
        game.SetLineup(game.PlayerOne,
            new Domain.ValueObjects.Lineup(
                DefaultLineup.Select(n => Domain.Factories.PieceFactory.Create(n, game.PlayerOne)).ToList()));

        var gameRepo = Substitute.For<IGameRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = BuildHandler(gameRepo);
        var command = new JoinGameCommand(game.Id, DefaultLineup);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: game transitioned to InProgress once both bots joined.
        Assert.Equal(GameStatus.InProgress, game.Status);
    }

    [Fact]
    public async Task Handle_SecondPlayer_SavesGameAndBotRegistration()
    {
        // Arrange
        var game = NewShell();

        game.SetLineup(game.PlayerOne,
            new Domain.ValueObjects.Lineup(
                DefaultLineup.Select(n => Domain.Factories.PieceFactory.Create(n, game.PlayerOne)).ToList()));

        var gameRepo = Substitute.For<IGameRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var botRegRepo = Substitute.For<IBotRegistrationRepository>();
        var handler = BuildHandler(gameRepo, botRegRepo);
        var command = new JoinGameCommand(game.Id, DefaultLineup);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: both game and bot-registration were persisted.
        await gameRepo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
        await botRegRepo.Received(1).SaveAsync(Arg.Any<Domain.BotRegistrations.BotRegistration>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FirstPlayer_SavesPersistenceForJoin()
    {
        // Arrange
        var game = NewShell();
        var gameRepo   = Substitute.For<IGameRepository>();
        var botRegRepo = Substitute.For<IBotRegistrationRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = BuildHandler(gameRepo, botRegRepo);
        var command = new JoinGameCommand(game.Id, DefaultLineup);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: both game and bot-registration were persisted for first player.
        await gameRepo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
        await botRegRepo.Received(1).SaveAsync(Arg.Any<Domain.BotRegistrations.BotRegistration>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenGameFull_ThrowsGameFullException()
    {
        // Arrange: both slots are already filled.
        var game = NewShell();
        var lineupP1 = new Domain.ValueObjects.Lineup(
            DefaultLineup.Select(n => Domain.Factories.PieceFactory.Create(n, game.PlayerOne)).ToList());
        var lineupP2 = new Domain.ValueObjects.Lineup(
            DefaultLineup.Select(n => Domain.Factories.PieceFactory.Create(n, game.PlayerTwo)).ToList());
        game.SetLineup(game.PlayerOne, lineupP1);
        game.SetLineup(game.PlayerTwo, lineupP2);

        var gameRepo = Substitute.For<IGameRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = BuildHandler(gameRepo);
        var command = new JoinGameCommand(game.Id, DefaultLineup);

        // Act & Assert: third join attempt throws GameFullException.
        await Assert.ThrowsAsync<GameFullException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_TwoSuccessiveJoins_ReturnDifferentTokens()
    {
        // Arrange
        var game = NewShell();
        var gameRepo = Substitute.For<IGameRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = BuildHandler(gameRepo);

        // Act
        var first  = await handler.Handle(new JoinGameCommand(game.Id, DefaultLineup), CancellationToken.None);
        var second = await handler.Handle(new JoinGameCommand(game.Id, DefaultLineup), CancellationToken.None);

        // Assert: each bot receives a unique token.
        Assert.NotEqual(first.Token, second.Token);
    }
}
