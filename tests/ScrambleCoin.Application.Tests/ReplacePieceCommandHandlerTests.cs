using Microsoft.Extensions.Logging;
using NSubstitute;
using ScrambleCoin.Application.Games.ReplacePiece;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Unit tests for <see cref="ReplacePieceCommandHandler"/>.
/// </summary>
public class ReplacePieceCommandHandlerTests
{
    private static (Game game, Guid p1, Guid p2, List<Piece> p1Pieces, List<Piece> p2Pieces) GameInPlacePhaseWithOnePiecePlaced()
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
        game.Start();
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        // Place first piece so it can be replaced
        game.PlacePiece(p1, p1Pieces[0].Id, new Position(0, 0));

        // Reset phase tracking: p2 skips so phase auto-advanced to MovePhase;
        // we need to get back to PlacePhase for the next turn.
        game.SkipPlacement(p2); // both acted → auto-advance to MovePhase
        game.AdvanceTurn();     // MovePhase → turn 2, CoinSpawn
        game.AdvancePhase();    // CoinSpawn → PlacePhase (turn 2)

        return (game, p1, p2, p1Pieces, p2Pieces);
    }

    [Fact]
    public async Task Handle_CallsReplacePieceAndSavesGame()
    {
        // Arrange
        var (game, p1, p2, p1Pieces, _) = GameInPlacePhaseWithOnePiecePlaced();

        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var logger = Substitute.For<ILogger<ReplacePieceCommandHandler>>();
        var handler = new ReplacePieceCommandHandler(repo, logger);

        // Replace p1Pieces[0] (on board at (0,0)) with p1Pieces[1] (off board) at (0,1)
        var command = new ReplacePieceCommand(game.Id, p1, p1Pieces[0].Id, p1Pieces[1].Id, new Position(0, 1));

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: old piece removed, new piece placed
        Assert.False(p1Pieces[0].IsOnBoard);
        Assert.True(p1Pieces[1].IsOnBoard);
        Assert.Equal(new Position(0, 1), p1Pieces[1].Position);

        // Assert: game was saved
        await repo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }
}
