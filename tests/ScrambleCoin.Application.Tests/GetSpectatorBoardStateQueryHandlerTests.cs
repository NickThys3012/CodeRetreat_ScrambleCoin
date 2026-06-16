using NSubstitute;
using ScrambleCoin.Application.Games.GetBoardState;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Unit tests for <see cref="GetSpectatorBoardStateQueryHandler"/> — Issue #107.
/// Focuses on the mapping of the spectator-only <see cref="BoardStateDto.IsSoloMode"/>
/// and <see cref="BoardStateDto.VillainId"/> fields used to colour villain pieces.
/// </summary>
public class GetSpectatorBoardStateQueryHandlerTests
{
    // ── Constants ─────────────────────────────────────────────────────────────

    private static readonly IReadOnlyList<string> DefaultLineup =
        ["Mickey", "Minnie", "Donald", "Goofy", "Scrooge"];

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Creates a started game (CoinSpawn phase) with both lineups set.</summary>
    private static Game NewStartedGame()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var pieces1 = DefaultLineup
            .Select(n => new Piece(Guid.NewGuid(), n, p1, EntryPointType.Borders, MovementType.Orthogonal, 3, 1))
            .ToList();
        var pieces2 = DefaultLineup
            .Select(n => new Piece(Guid.NewGuid(), n, p2, EntryPointType.Borders, MovementType.Orthogonal, 3, 1))
            .ToList();

        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(pieces1));
        game.SetLineup(p2, new Lineup(pieces2));
        game.Start(); // → TurnNumber = 1, Phase = CoinSpawn

        return game;
    }

    private static GetSpectatorBoardStateQueryHandler BuildHandler(IGameRepository gameRepo)
        => new(gameRepo);

    // ── Solo mode ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_SoloGame_SetsIsSoloModeTrue()
    {
        // Arrange
        var game = NewStartedGame();
        game.GameMode = GameMode.Solo;
        game.VillainId = "elsa";

        var gameRepo = Substitute.For<IGameRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = BuildHandler(gameRepo);

        // Act
        var dto = await handler.Handle(new GetSpectatorBoardStateQuery(game.Id), CancellationToken.None);

        // Assert
        Assert.True(dto.IsSoloMode);
    }

    [Fact]
    public async Task Handle_SoloGame_MapsVillainId()
    {
        // Arrange
        var game = NewStartedGame();
        game.GameMode = GameMode.Solo;
        game.VillainId = "elsa";

        var gameRepo = Substitute.For<IGameRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = BuildHandler(gameRepo);

        // Act
        var dto = await handler.Handle(new GetSpectatorBoardStateQuery(game.Id), CancellationToken.None);

        // Assert
        Assert.Equal("elsa", dto.VillainId);
    }

    // ── Bot-vs-bot / standard mode ────────────────────────────────────────────

    [Fact]
    public async Task Handle_StandardGame_SetsIsSoloModeFalse()
    {
        // Arrange — default GameMode is Standard (bot vs bot)
        var game = NewStartedGame();

        var gameRepo = Substitute.For<IGameRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = BuildHandler(gameRepo);

        // Act
        var dto = await handler.Handle(new GetSpectatorBoardStateQuery(game.Id), CancellationToken.None);

        // Assert
        Assert.False(dto.IsSoloMode);
    }

    [Fact]
    public async Task Handle_StandardGame_LeavesVillainIdNull()
    {
        // Arrange — a stray VillainId must NOT leak through when the game is not solo
        var game = NewStartedGame();
        game.GameMode = GameMode.Standard;
        game.VillainId = "elsa";

        var gameRepo = Substitute.For<IGameRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = BuildHandler(gameRepo);

        // Act
        var dto = await handler.Handle(new GetSpectatorBoardStateQuery(game.Id), CancellationToken.None);

        // Assert
        Assert.Null(dto.VillainId);
    }
}
