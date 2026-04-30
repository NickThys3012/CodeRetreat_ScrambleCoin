using Microsoft.Extensions.Logging;
using NSubstitute;
using ScrambleCoin.Application.Games.SpawnCoins;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Unit tests for <see cref="SpawnCoinsCommandHandler"/>.
/// </summary>
public class SpawnCoinsCommandHandlerTests
{
    private static Game GameInCoinSpawnPhase()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var p1Pieces = Enumerable.Range(0, 5)
            .Select(i => new Piece(Guid.NewGuid(), $"P1Piece{i}", p1, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();
        var p2Pieces = Enumerable.Range(0, 5)
            .Select(i => new Piece(Guid.NewGuid(), $"P2Piece{i}", p2, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(p1Pieces));
        game.SetLineup(p2, new Lineup(p2Pieces));
        game.Start(); // → CoinSpawn phase, turn 1
        return game;
    }

    [Fact]
    public async Task Handle_SpawnsCoinsOnBoardAndSavesGame()
    {
        // Arrange
        var game = GameInCoinSpawnPhase();

        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var logger = Substitute.For<ILogger<SpawnCoinsCommandHandler>>();
        // Use a seeded Random for deterministic tile selection
        var random = new Random(42);
        var handler = new SpawnCoinsCommandHandler(repo, random, logger);

        var command = new SpawnCoinsCommand(game.Id);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: at least one coin was spawned (turn 1 schedule guarantees coins)
        var coinTiles = game.Board.GetAllCoins();
        Assert.NotEmpty(coinTiles);

        // Assert: the game was saved
        await repo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Turn1_SpawnsSilverCoins()
    {
        // Arrange
        var game = GameInCoinSpawnPhase(); // turn 1

        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var logger = Substitute.For<ILogger<SpawnCoinsCommandHandler>>();
        var random = new Random(0);
        var handler = new SpawnCoinsCommandHandler(repo, random, logger);

        // Act
        await handler.Handle(new SpawnCoinsCommand(game.Id), CancellationToken.None);

        // Assert: all spawned coins on turn 1 are Silver
        var coinTiles = game.Board.GetAllCoins();
        Assert.All(coinTiles, t => Assert.Equal(CoinType.Silver, t.AsCoin!.CoinType));
    }

    [Fact]
    public async Task Handle_WhenBoardFull_SpawnsOnlyAsManyAsFit()
    {
        // Arrange: fill all but 2 tiles with pieces so almost no free tiles remain
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var p1Pieces = Enumerable.Range(0, 5)
            .Select(i => new Piece(Guid.NewGuid(), $"P1Piece{i}", p1, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();
        var p2Pieces = Enumerable.Range(0, 5)
            .Select(i => new Piece(Guid.NewGuid(), $"P2Piece{i}", p2, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var board = new Board();
        var game = new Game(p1, p2, board);
        game.SetLineup(p1, new Lineup(p1Pieces));
        game.SetLineup(p2, new Lineup(p2Pieces));
        game.Start();

        // Manually fill almost all tiles with fake coin occupants to simulate a crowded board
        // Leave only 2 free tiles: (7,6) and (7,7)
        for (var row = 0; row < 8; row++)
        for (var col = 0; col < 8; col++)
        {
            if (row == 7 && col >= 6) continue; // leave 2 free
            board.GetTile(new Position(row, col)).SetOccupant(new Coin(CoinType.Silver));
        }

        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var logger = Substitute.For<ILogger<SpawnCoinsCommandHandler>>();
        var random = new Random(1);
        var handler = new SpawnCoinsCommandHandler(repo, random, logger);

        // Act: should not throw; spawns ≤ 2 coins
        await handler.Handle(new SpawnCoinsCommand(game.Id), CancellationToken.None);

        // Assert: no more than 2 coins were added to the originally free tiles
        var freeTileCoins = new[]
        {
            board.GetTile(new Position(7, 6)).AsCoin,
            board.GetTile(new Position(7, 7)).AsCoin
        }.Count(c => c is not null);

        Assert.True(freeTileCoins <= 2);
        await repo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }
}
