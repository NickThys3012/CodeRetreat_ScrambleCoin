using Microsoft.Extensions.Logging;
using NSubstitute;
using ScrambleCoin.Application.Games.PlacePiece;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Unit tests for <see cref="PlacePieceCommandHandler"/>.
/// </summary>
public class PlacePieceCommandHandlerTests
{
    [Fact]
    public async Task Handle_CallsPlacePieceAndSavesGame()
    {
        // Arrange
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var board = new Board();

        var p1Pieces = Enumerable.Range(0, 5)
            .Select(i => new Piece(Guid.NewGuid(), $"P1Piece{i}", p1, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();
        var p2Pieces = Enumerable.Range(0, 5)
            .Select(i => new Piece(Guid.NewGuid(), $"P2Piece{i}", p2, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var game = new Game(p1, p2, board);
        game.SetLineup(p1, new Lineup(p1Pieces));
        game.SetLineup(p2, new Lineup(p2Pieces));
        game.Start();
        game.AdvancePhase(); // → PlacePhase

        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var logger = Substitute.For<ILogger<PlacePieceCommandHandler>>();
        var handler = new PlacePieceCommandHandler(repo, logger);

        var command = new PlacePieceCommand(game.Id, p1, p1Pieces[0].Id, new Position(0, 0));

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: a piece was placed on the board.
        Assert.True(p1Pieces[0].IsOnBoard);

        // Assert: the game was saved.
        await repo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }
}
