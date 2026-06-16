using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Tests;

/// <summary>
/// Unit tests for the <see cref="Game.HasPieceMovedThisTurn"/> accessor (Issue #41).
/// The villain strategy uses this to find the next unmoved on-board piece, so it must reflect
/// the move/skip state accurately and reset between turns.
/// </summary>
public class HasPieceMovedThisTurnTests
{
    private static (Game game, Guid p1, Guid p2, Piece p1Piece, Piece p2Piece) GameInMovePhase(
        Position? p1Pos = null,
        Position? p2Pos = null)
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var p1Piece = new Piece(Guid.NewGuid(), "P1Mover", p1,
            EntryPointType.Borders, MovementType.Orthogonal, 3, 1);
        var p1Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"P1Fill{i}", p1, EntryPointType.Borders, MovementType.Orthogonal, 1, 1));

        var p2Piece = new Piece(Guid.NewGuid(), "P2Mover", p2,
            EntryPointType.Borders, MovementType.Orthogonal, 1, 1);
        var p2Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"P2Fill{i}", p2, EntryPointType.Borders, MovementType.Orthogonal, 1, 1));

        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(new[] { p1Piece }.Concat(p1Fill)));
        game.SetLineup(p2, new Lineup(new[] { p2Piece }.Concat(p2Fill)));
        game.Start();
        game.AdvancePhase(); // CoinSpawn → PlacePhase
        game.PlacePiece(p1, p1Piece.Id, p1Pos ?? new Position(0, 3));
        game.PlacePiece(p2, p2Piece.Id, p2Pos ?? new Position(7, 3)); // → MovePhase

        return (game, p1, p2, p1Piece, p2Piece);
    }

    [Fact]
    public void HasPieceMovedThisTurn_BeforeAnyMove_ReturnsFalse()
    {
        var (game, _, _, p1Piece, _) = GameInMovePhase();

        Assert.False(game.HasPieceMovedThisTurn(p1Piece.Id));
    }

    [Fact]
    public void HasPieceMovedThisTurn_AfterMovingPiece_ReturnsTrue()
    {
        var (game, p1, _, p1Piece, _) = GameInMovePhase();

        game.MovePiece(p1, p1Piece.Id, new List<IReadOnlyList<Position>>
        {
            new List<Position> { new(0, 4) }
        });

        Assert.True(game.HasPieceMovedThisTurn(p1Piece.Id));
    }

    [Fact]
    public void HasPieceMovedThisTurn_AfterSkipMovement_ReturnsTrueForAllOnBoardPieces()
    {
        var (game, p1, _, p1Piece, _) = GameInMovePhase();

        game.SkipMovement(p1);

        Assert.True(game.HasPieceMovedThisTurn(p1Piece.Id));
    }

    [Fact]
    public void HasPieceMovedThisTurn_ResetsAtStartOfNextMovePhase()
    {
        var (game, p1, p2, p1Piece, _) = GameInMovePhase();

        // Complete the move phase for both players to advance to the next turn.
        game.SkipMovement(p1);
        game.SkipMovement(p2);

        // Advance through turn 2's CoinSpawn and PlacePhase into its MovePhase, where the
        // moved-piece markers are cleared.
        game.AdvancePhase();      // CoinSpawn → PlacePhase
        game.SkipPlacement(p1);
        game.SkipPlacement(p2);   // → MovePhase (markers cleared)

        Assert.False(game.HasPieceMovedThisTurn(p1Piece.Id));
    }

    [Fact]
    public void HasPieceMovedThisTurn_UnknownPieceId_ReturnsFalse()
    {
        var (game, _, _, _, _) = GameInMovePhase();

        Assert.False(game.HasPieceMovedThisTurn(Guid.NewGuid()));
    }
}
