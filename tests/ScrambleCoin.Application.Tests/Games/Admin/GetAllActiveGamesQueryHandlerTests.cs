using NSubstitute;
using ScrambleCoin.Application.Games.Admin;
using ScrambleCoin.Application.Interfaces;

namespace ScrambleCoin.Application.Tests.Games.Admin;

/// <summary>
/// Unit tests for <see cref="GetAllActiveGamesQueryHandler"/> (Issue #57).
/// </summary>
public sealed class GetAllActiveGamesQueryHandlerTests
{
    private static GetAllActiveGamesQueryHandler BuildHandler(IGameRepository repo) =>
        new(repo);

    [Fact]
    public async Task Handle_ReturnsActiveGamesFromRepository()
    {
        // Arrange
        var expected = new List<ActiveGameSummaryDto>
        {
            new(
                GameId:        Guid.NewGuid(),
                PlayerOne:     Guid.NewGuid(),
                PlayerTwo:     Guid.NewGuid(),
                Status:        "InProgress",
                TurnNumber:    2,
                Phase:         "MovePhase",
                ScorePlayerOne: 5,
                ScorePlayerTwo: 3,
                LastMoveAt:    DateTimeOffset.UtcNow),
            new(
                GameId:        Guid.NewGuid(),
                PlayerOne:     Guid.NewGuid(),
                PlayerTwo:     Guid.NewGuid(),
                Status:        "WaitingForBots",
                TurnNumber:    0,
                Phase:         null,
                ScorePlayerOne: 0,
                ScorePlayerTwo: 0,
                LastMoveAt:    DateTimeOffset.UtcNow)
        }.AsReadOnly();

        var repo = Substitute.For<IGameRepository>();
        repo.GetAllActiveAsync(Arg.Any<CancellationToken>())
            .Returns(expected);

        var handler = BuildHandler(repo);

        // Act
        var result = await handler.Handle(new GetAllActiveGamesQuery(), CancellationToken.None);

        // Assert: the handler passes through exactly what the repository returns.
        Assert.Equal(expected.Count, result.Count);
        Assert.Equal(expected[0].GameId, result[0].GameId);
        Assert.Equal(expected[1].GameId, result[1].GameId);
    }

    [Fact]
    public async Task Handle_WhenNoActiveGames_ReturnsEmptyList()
    {
        // Arrange
        var repo = Substitute.For<IGameRepository>();
        repo.GetAllActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ActiveGameSummaryDto>().AsReadOnly());

        var handler = BuildHandler(repo);

        // Act
        var result = await handler.Handle(new GetAllActiveGamesQuery(), CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_DelegatesDirectlyToRepository()
    {
        // Arrange
        var repo = Substitute.For<IGameRepository>();
        repo.GetAllActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ActiveGameSummaryDto>().AsReadOnly());

        var handler = BuildHandler(repo);

        // Act
        await handler.Handle(new GetAllActiveGamesQuery(), CancellationToken.None);

        // Assert: the handler calls the repository exactly once.
        await repo.Received(1).GetAllActiveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PreservesAllDtoFields()
    {
        // Arrange
        var gameId    = Guid.NewGuid();
        var playerOne = Guid.NewGuid();
        var playerTwo = Guid.NewGuid();
        var lastMove  = DateTimeOffset.UtcNow.AddMinutes(-3);

        var summary = new ActiveGameSummaryDto(
            GameId:        gameId,
            PlayerOne:     playerOne,
            PlayerTwo:     playerTwo,
            Status:        "InProgress",
            TurnNumber:    4,
            Phase:         "PlacePhase",
            ScorePlayerOne: 10,
            ScorePlayerTwo: 7,
            LastMoveAt:    lastMove);

        var repo = Substitute.For<IGameRepository>();
        repo.GetAllActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ActiveGameSummaryDto> { summary }.AsReadOnly());

        var handler = BuildHandler(repo);

        // Act
        var result = await handler.Handle(new GetAllActiveGamesQuery(), CancellationToken.None);

        // Assert: every field is preserved unchanged.
        var dto = result.Single();
        Assert.Equal(gameId,       dto.GameId);
        Assert.Equal(playerOne,    dto.PlayerOne);
        Assert.Equal(playerTwo,    dto.PlayerTwo);
        Assert.Equal("InProgress", dto.Status);
        Assert.Equal(4,            dto.TurnNumber);
        Assert.Equal("PlacePhase", dto.Phase);
        Assert.Equal(10,           dto.ScorePlayerOne);
        Assert.Equal(7,            dto.ScorePlayerTwo);
        Assert.Equal(lastMove,     dto.LastMoveAt);
    }
}
