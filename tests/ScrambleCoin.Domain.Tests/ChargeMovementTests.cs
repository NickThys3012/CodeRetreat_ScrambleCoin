using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Events;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.Factories;
using ScrambleCoin.Domain.Obstacles;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Tests;

/// <summary>
/// Unit tests for Charge movement ability (Issue #45).
/// Charge pieces (Pumbaa, WALL•E) move in one direction until hitting an obstacle,
/// fence, another piece, or board edge. Coins are collected at every position along the path.
/// </summary>
public sealed class ChargeMovementTests
{
    private static (Game game, Guid p1, Guid p2, Piece chargePiece, Piece filler2) GameInMovePhaseWithChargePiece(
        Position? chargeStartPos = null)
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var board = new Board();

        var chargePos = chargeStartPos ?? new Position(0, 0);
        var p2Pos = new Position(7, 7);

        var chargePiece = PieceFactory.Create("Pumbaa", p1);
        var p1Fillers = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"P1Filler{i}", p1, 
                EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var p2Fillers = Enumerable.Range(0, 5)
            .Select(i => new Piece(Guid.NewGuid(), $"P2Filler{i}", p2, 
                EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var game = new Game(p1, p2, board);
        game.SetLineup(p1, new Lineup(new[] { chargePiece }.Concat(p1Fillers)));
        game.SetLineup(p2, new Lineup(p2Fillers));
        game.Start();
        game.AdvancePhase();

        game.PlacePiece(p1, chargePiece.Id, chargePos);
        game.PlacePiece(p2, p2Fillers[0].Id, p2Pos);

        var placedChargePiece = board.GetTile(chargePos).AsPiece!;

        return (game, p1, p2, placedChargePiece, p2Fillers[0]);
    }

    private static IReadOnlyList<IReadOnlyList<Position>> BuildChargeSegment(Position direction)
    {
        var segment = (IReadOnlyList<Position>)new List<Position> { direction }.AsReadOnly();
        return new List<IReadOnlyList<Position>> { segment }.AsReadOnly();
    }

    [Fact]
    public void ChargeMovement_ToEdgeWithNoClear_SlidesPieceToEdge()
    {
        var (game, p1, _, chargePiece, _) = GameInMovePhaseWithChargePiece(new Position(0, 0));
        game.MovePiece(p1, chargePiece.Id, BuildChargeSegment(new Position(0, 1)));
        Assert.Equal(new Position(0, 7), chargePiece.Position);
    }

    [Fact]
    public void ChargeMovement_ToEdge_RaisesPieceMovedEvent()
    {
        var (game, p1, _, chargePiece, _) = GameInMovePhaseWithChargePiece(new Position(0, 0));
        game.MovePiece(p1, chargePiece.Id, BuildChargeSegment(new Position(0, 1)));
        var evt = game.DomainEvents.OfType<PieceMoved>().SingleOrDefault();
        Assert.NotNull(evt);
        Assert.Equal(chargePiece.Id, evt.PieceId);
    }

    [Fact]
    public void ChargeMovement_BlockedByRock_StopsBeforeObstacle()
    {
        var (game, p1, _, chargePiece, _) = GameInMovePhaseWithChargePiece(new Position(0, 0));
        game.Board.AddRock(new Rock(new Position(0, 5)));
        game.MovePiece(p1, chargePiece.Id, BuildChargeSegment(new Position(0, 1)));
        Assert.Equal(new Position(0, 4), chargePiece.Position);
    }

    [Fact]
    public void ChargeMovement_BlockedByFence_StopsBeforeFence()
    {
        var (game, p1, _, chargePiece, _) = GameInMovePhaseWithChargePiece(new Position(0, 0));
        game.Board.AddFence(new Fence(new Position(0, 5), new Position(0, 6)));
        game.MovePiece(p1, chargePiece.Id, BuildChargeSegment(new Position(0, 1)));
        Assert.True(chargePiece.Position.Col <= 5);
    }

    [Fact]
    public void ChargeMovement_FirstTileBlocked_PieceStaysInPlace()
    {
        var (game, p1, _, chargePiece, _) = GameInMovePhaseWithChargePiece(new Position(0, 0));
        game.Board.AddRock(new Rock(new Position(0, 1)));
        game.MovePiece(p1, chargePiece.Id, BuildChargeSegment(new Position(0, 1)));
        Assert.Equal(new Position(0, 0), chargePiece.Position);
    }

    [Fact]
    public void ChargeMovement_CollectsCoinsAlongPath()
    {
        var (game, p1, _, chargePiece, _) = GameInMovePhaseWithChargePiece(new Position(0, 0));
        game.Board.GetTile(new Position(0, 2)).SetOccupant(new Coin(CoinType.Silver));
        game.Board.GetTile(new Position(0, 4)).SetOccupant(new Coin(CoinType.Gold));
        game.Board.GetTile(new Position(0, 6)).SetOccupant(new Coin(CoinType.Silver));
        game.MovePiece(p1, chargePiece.Id, BuildChargeSegment(new Position(0, 1)));
        var coinEvents = game.DomainEvents.OfType<CoinCollected>().ToList();
        Assert.Equal(3, coinEvents.Count);
    }

    [Fact]
    public void ChargeMovement_Score_UpdatedCorrectly()
    {
        var (game, p1, _, chargePiece, _) = GameInMovePhaseWithChargePiece(new Position(0, 0));
        game.Board.GetTile(new Position(0, 2)).SetOccupant(new Coin(CoinType.Silver));
        game.Board.GetTile(new Position(0, 4)).SetOccupant(new Coin(CoinType.Gold));
        game.Board.GetTile(new Position(0, 6)).SetOccupant(new Coin(CoinType.Silver));
        game.MovePiece(p1, chargePiece.Id, BuildChargeSegment(new Position(0, 1)));
        Assert.Equal(5, game.Scores[p1]);
    }

    [Fact]
    public void ChargeMovement_DiagonalDirection_Throws()
    {
        var (game, p1, _, chargePiece, _) = GameInMovePhaseWithChargePiece(new Position(0, 0));
        var diagonalDir = new Position(1, 1);
        var ex = Assert.Throws<DomainException>(() =>
            game.MovePiece(p1, chargePiece.Id, BuildChargeSegment(diagonalDir)));
        Assert.Contains("not orthogonal", ex.Message);
    }

    [Fact]
    public void ChargeMovement_MultipleDirections_Throws()
    {
        var (game, p1, _, chargePiece, _) = GameInMovePhaseWithChargePiece(new Position(0, 0));
        var twoDirections = new List<IReadOnlyList<Position>>
        {
            new List<Position> { new Position(0, 1), new Position(1, 1) }.AsReadOnly()
        }.AsReadOnly();
        var ex = Assert.Throws<DomainException>(() =>
            game.MovePiece(p1, chargePiece.Id, twoDirections));
        Assert.Contains("exactly 1 direction", ex.Message);
    }

    [Fact]
    public void ChargeMovement_North_SlidesCorrectly()
    {
        var (game, p1, _, chargePiece, _) = GameInMovePhaseWithChargePiece(new Position(7, 0));
        game.MovePiece(p1, chargePiece.Id, BuildChargeSegment(new Position(6, 0)));
        Assert.Equal(new Position(0, 0), chargePiece.Position);
    }

    [Fact]
    public void Pumbaa_HasChargeMovement()
    {
        var playerId = Guid.NewGuid();
        var piece = PieceFactory.Create("Pumbaa", playerId);
        Assert.Equal(MovementType.Charge, piece.MovementType);
    }

    [Fact]
    public void WallE_HasChargeMovement()
    {
        var playerId = Guid.NewGuid();
        var piece = PieceFactory.Create("WALL•E", playerId);
        Assert.Equal(MovementType.Charge, piece.MovementType);
    }

    [Fact]
    public void ChargeMovement_EmptyPath_WhenBlocked()
    {
        var (game, p1, _, chargePiece, _) = GameInMovePhaseWithChargePiece(new Position(0, 0));
        game.Board.AddRock(new Rock(new Position(0, 1)));
        game.MovePiece(p1, chargePiece.Id, BuildChargeSegment(new Position(0, 1)));
        var movedEvent = game.DomainEvents.OfType<PieceMoved>().SingleOrDefault();
        Assert.NotNull(movedEvent);
        Assert.Empty(movedEvent.Path);
    }

    [Fact]
    public void ChargeMovement_FullPath_ContainsIntermediatePositions()
    {
        var (game, p1, _, chargePiece, _) = GameInMovePhaseWithChargePiece(new Position(0, 0));
        game.MovePiece(p1, chargePiece.Id, BuildChargeSegment(new Position(0, 1)));
        var movedEvent = game.DomainEvents.OfType<PieceMoved>().SingleOrDefault();
        Assert.NotNull(movedEvent);
        Assert.Equal(7, movedEvent.Path.Count);
    }
}
