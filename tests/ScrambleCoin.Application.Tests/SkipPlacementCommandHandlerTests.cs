using Microsoft.Extensions.Logging;
using NSubstitute;
using ScrambleCoin.Application.Games.SkipPlacement;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Unit tests for <see cref="SkipPlacementCommandHandler"/>.
/// </summary>
public class SkipPlacementCommandHandlerTests
{
    [Fact]
    public async Task Handle_CallsSkipPlacementAndSavesGame()
    {
        // Arrange
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
        game.Start();
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var logger = Substitute.For<ILogger<SkipPlacementCommandHandler>>();
        var handler = new SkipPlacementCommandHandler(repo, logger);

        var command = new SkipPlacementCommand(game.Id, p1);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: the game is still in PlacePhase (only p1 skipped; p2 hasn't acted yet)
        Assert.Equal(TurnPhase.PlacePhase, game.CurrentPhase);

        // Assert: the game was saved
        await repo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_BothPlayersSkip_AutoAdvancesToMovePhase()
    {
        // Arrange
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
        game.Start();
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var logger = Substitute.For<ILogger<SkipPlacementCommandHandler>>();
        var handler = new SkipPlacementCommandHandler(repo, logger);

        // Act: both players skip
        await handler.Handle(new SkipPlacementCommand(game.Id, p1), CancellationToken.None);
        await handler.Handle(new SkipPlacementCommand(game.Id, p2), CancellationToken.None);

        // Assert: phase auto-advanced to MovePhase
        Assert.Equal(TurnPhase.MovePhase, game.CurrentPhase);

        await repo.Received(2).SaveAsync(game, Arg.Any<CancellationToken>());
    }
}
