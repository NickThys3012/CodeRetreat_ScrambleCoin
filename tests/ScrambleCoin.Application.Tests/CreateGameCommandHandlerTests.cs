using Microsoft.Extensions.Logging;
using NSubstitute;
using ScrambleCoin.Application.Games.CreateGame;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Unit tests for <see cref="CreateGameCommandHandler"/> (Issue #37).
/// </summary>
public class CreateGameCommandHandlerTests
{
    private static CreateGameCommandHandler BuildHandler(IGameRepository repo)
    {
        var logger = Substitute.For<ILogger<CreateGameCommandHandler>>();
        return new CreateGameCommandHandler(repo, logger);
    }

    [Fact]
    public async Task Handle_CreatesGame_AndSavesToRepository()
    {
        // Arrange
        var repo = Substitute.For<IGameRepository>();
        var handler = BuildHandler(repo);
        var command = new CreateGameCommand();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: SaveAsync was called once with a non-null game.
        await repo.Received(1).SaveAsync(Arg.Is<Game>(g => g != null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsResult_WithNonEmptyGameId()
    {
        // Arrange
        var repo = Substitute.For<IGameRepository>();
        var handler = BuildHandler(repo);
        var command = new CreateGameCommand();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: the returned GameId is a real (non-empty) GUID.
        Assert.NotEqual(Guid.Empty, result.GameId);
    }

    [Fact]
    public async Task Handle_SavedGame_HasWaitingForBotsStatus()
    {
        // Arrange
        Game? capturedGame = null;
        var repo = Substitute.For<IGameRepository>();
        await repo.SaveAsync(Arg.Do<Game>(g => capturedGame = g), Arg.Any<CancellationToken>());

        var handler = BuildHandler(repo);
        var command = new CreateGameCommand();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: the persisted game starts in WaitingForBots.
        Assert.NotNull(capturedGame);
        Assert.Equal(GameStatus.WaitingForBots, capturedGame!.Status);
    }

    [Fact]
    public async Task Handle_SavedGame_IdMatchesReturnedGameId()
    {
        // Arrange
        Game? capturedGame = null;
        var repo = Substitute.For<IGameRepository>();
        await repo.SaveAsync(Arg.Do<Game>(g => capturedGame = g), Arg.Any<CancellationToken>());

        var handler = BuildHandler(repo);
        var command = new CreateGameCommand();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: the ID in the result matches the persisted game.
        Assert.NotNull(capturedGame);
        Assert.Equal(capturedGame!.Id, result.GameId);
    }
}
