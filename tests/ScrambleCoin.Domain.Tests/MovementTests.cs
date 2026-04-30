using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Events;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.Obstacles;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Tests;

/// <summary>
/// Unit tests for <see cref="Game.MovePiece"/> — basic movement and coin collection (Issue #11).
/// </summary>
public class MovementTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a game in MovePhase with exactly one piece per player already on the board.
    /// P1's piece has the specified movement properties; P2's piece is a plain orthogonal piece.
    /// </summary>
    private static (Game game, Guid p1, Guid p2, Piece p1Piece, Piece p2Piece) GameInMovePhaseWithOnePieceEach(
        MovementType p1MovementType = MovementType.Orthogonal,
        int p1MaxDistance = 3,
        int p1MovesPerTurn = 1,
        Position? p1StartPos = null,
        Position? p2StartPos = null)
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var board = new Board();

        var actualP1Pos = p1StartPos ?? new Position(0, 3);
        var actualP2Pos = p2StartPos ?? new Position(7, 3);

        var p1Piece = new Piece(Guid.NewGuid(), "P1Mover", p1,
            EntryPointType.Borders, p1MovementType, p1MaxDistance, p1MovesPerTurn);
        var p1Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"P1Fill{i}", p1, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var p2Piece = new Piece(Guid.NewGuid(), "P2Mover", p2,
            EntryPointType.Borders, MovementType.Orthogonal, 1, 1);
        var p2Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"P2Fill{i}", p2, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var game = new Game(p1, p2, board);
        game.SetLineup(p1, new Lineup(new[] { p1Piece }.Concat(p1Fill)));
        game.SetLineup(p2, new Lineup(new[] { p2Piece }.Concat(p2Fill)));
        game.Start();
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        // Placing both pieces auto-advances to MovePhase.
        game.PlacePiece(p1, p1Piece.Id, actualP1Pos);
        game.PlacePiece(p2, p2Piece.Id, actualP2Pos);

        return (game, p1, p2, p1Piece, p2Piece);
    }

    /// <summary>
    /// Builds a single-segment move list containing the provided step positions.
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<Position>> BuildSegments(params Position[] steps)
    {
        var segment = (IReadOnlyList<Position>)steps.ToList().AsReadOnly();
        return new List<IReadOnlyList<Position>> { segment }.AsReadOnly();
    }

    // ── Test 1: Orthogonal move ───────────────────────────────────────────────

    [Fact]
    public void OrthogonalMove_UpdatesPositionAndRaisesPieceMovedEvent()
    {
        // Arrange: piece at (0,3), board clear ahead
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithOnePieceEach();

        // Act: move right 2 steps → (0,4) → (0,5)
        game.MovePiece(p1, p1Piece.Id, BuildSegments(new Position(0, 4), new Position(0, 5)));

        // Assert: position updated
        Assert.Equal(new Position(0, 5), p1Piece.Position);

        // Assert: PieceMoved event raised
        var evt = game.DomainEvents.OfType<PieceMoved>().SingleOrDefault();
        Assert.NotNull(evt);
    }

    [Fact]
    public void OrthogonalMove_RaisesPieceMovedEvent()
    {
        // Arrange
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithOnePieceEach();

        // Act
        game.MovePiece(p1, p1Piece.Id, BuildSegments(new Position(0, 4)));

        // Assert
        var evt = game.DomainEvents.OfType<PieceMoved>().SingleOrDefault();
        Assert.NotNull(evt);
        Assert.Equal(p1Piece.Id, evt.PieceId);
        Assert.Equal(new Position(0, 3), evt.From);
        Assert.Equal(new Position(0, 4), evt.To);
    }

    // ── Test 2: Diagonal move ─────────────────────────────────────────────────

    [Fact]
    public void DiagonalMove_UpdatesPosition()
    {
        // Arrange: diagonal piece at (0,3)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithOnePieceEach(
            p1MovementType: MovementType.Diagonal,
            p1MaxDistance: 3);

        // Act: move diagonally (0,3)→(1,4)→(2,5)
        game.MovePiece(p1, p1Piece.Id, BuildSegments(new Position(1, 4), new Position(2, 5)));

        // Assert
        Assert.Equal(new Position(2, 5), p1Piece.Position);
    }

    [Fact]
    public void DiagonalMove_RaisesPieceMovedEvent()
    {
        // Arrange
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithOnePieceEach(
            p1MovementType: MovementType.Diagonal);

        // Act
        game.MovePiece(p1, p1Piece.Id, BuildSegments(new Position(1, 4)));

        // Assert
        var evt = game.DomainEvents.OfType<PieceMoved>().SingleOrDefault();
        Assert.NotNull(evt);
        Assert.Equal(p1Piece.Id, evt.PieceId);
    }

    // ── Test 3: AnyDirection move (mixed steps) ───────────────────────────────

    [Fact]
    public void AnyDirectionMove_WithMixedOrthogonalAndDiagonalSteps_UpdatesPosition()
    {
        // Arrange: AnyDirection piece at (0,3)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithOnePieceEach(
            p1MovementType: MovementType.AnyDirection,
            p1MaxDistance: 3);

        // Act: step right (orthogonal) then step diagonally down-right
        // (0,3)→(0,4)→(1,5)
        game.MovePiece(p1, p1Piece.Id, BuildSegments(new Position(0, 4), new Position(1, 5)));

        // Assert
        Assert.Equal(new Position(1, 5), p1Piece.Position);
    }

    // ── Test 4: Orthogonal piece rejects diagonal step ────────────────────────

    [Fact]
    public void OrthogonalPiece_WithDiagonalStep_ThrowsDomainException()
    {
        // Arrange: orthogonal piece at (0,3)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithOnePieceEach();

        // Act & Assert: step (0,3)→(1,4) is diagonal — must be rejected
        Assert.Throws<DomainException>(() =>
            game.MovePiece(p1, p1Piece.Id, BuildSegments(new Position(1, 4))));
    }

    // ── Test 5: Diagonal piece rejects orthogonal step ────────────────────────

    [Fact]
    public void DiagonalPiece_WithOrthogonalStep_ThrowsDomainException()
    {
        // Arrange: diagonal piece at (0,3)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithOnePieceEach(
            p1MovementType: MovementType.Diagonal);

        // Act & Assert: step (0,3)→(0,4) is orthogonal — must be rejected
        Assert.Throws<DomainException>(() =>
            game.MovePiece(p1, p1Piece.Id, BuildSegments(new Position(0, 4))));
    }

    // ── Test 6: Rock obstacle blocks movement ─────────────────────────────────

    [Fact]
    public void Move_IntoRockTile_ThrowsDomainException()
    {
        // Arrange: piece at (0,3), rock at (0,4)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithOnePieceEach();
        game.Board.AddRock(new Rock(new Position(0, 4)));

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            game.MovePiece(p1, p1Piece.Id, BuildSegments(new Position(0, 4))));
    }

    [Fact]
    public void Move_IntoLakeTile_ThrowsDomainException()
    {
        // Arrange: piece at (0,3), lake covering (2,4)–(3,5)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithOnePieceEach(
            p1StartPos: new Position(0, 4));
        // Lake top-left (1,4) → covers (1,4),(1,5),(2,4),(2,5)
        game.Board.AddLake(new Lake(new Position(1, 4)));

        // Act & Assert: a lake blocks (0,4)→(1,4)
        Assert.Throws<DomainException>(() =>
            game.MovePiece(p1, p1Piece.Id, BuildSegments(new Position(1, 4))));
    }

    // ── Test 7: Piece-occupied tile blocks movement ───────────────────────────

    [Fact]
    public void Move_IntoTileOccupiedByAnotherPiece_ThrowsDomainException()
    {
        // Arrange: P1 at (0,3), P2 at (0,4) — directly adjacent
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithOnePieceEach(
            p2StartPos: new Position(0, 4));

        // Act & Assert: moving right into P2's tile must throw
        Assert.Throws<DomainException>(() =>
            game.MovePiece(p1, p1Piece.Id, BuildSegments(new Position(0, 4))));
    }

    // ── Test 8: Silver coin collected on a path ─────────────────────────────────

    [Fact]
    public void Move_ThroughSilverCoin_RemovesCoinAndIncreasesScoreByOne()
    {
        // Arrange: piece at (0,3), silver coin at (0,4)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithOnePieceEach();
        game.Board.GetTile(new Position(0, 4)).SetOccupant(new Coin(CoinType.Silver));

        // Act: move through coin tile (0,3)→(0,4)→(0,5)
        game.MovePiece(p1, p1Piece.Id, BuildSegments(new Position(0, 4), new Position(0, 5)));

        // Assert: score +1
        Assert.Equal(1, game.Scores[p1]);

        // Assert: coin removed from board
        Assert.Null(game.Board.GetTile(new Position(0, 4)).AsCoin);
    }

    [Fact]
    public void Move_ThroughSilverCoin_CoinRemovedFromBoard()
    {
        // Arrange
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithOnePieceEach();
        var coinTile = game.Board.GetTile(new Position(0, 4));
        coinTile.SetOccupant(new Coin(CoinType.Silver));

        // Act
        game.MovePiece(p1, p1Piece.Id, BuildSegments(new Position(0, 4), new Position(0, 5)));

        // Assert: coin removed
        Assert.Null(coinTile.AsCoin);
    }

    [Fact]
    public void Move_ThroughSilverCoin_RaisesCoinCollectedEvent()
    {
        // Arrange
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithOnePieceEach();
        game.Board.GetTile(new Position(0, 4)).SetOccupant(new Coin(CoinType.Silver));

        // Act
        game.MovePiece(p1, p1Piece.Id, BuildSegments(new Position(0, 4), new Position(0, 5)));

        // Assert
        var evt = game.DomainEvents.OfType<CoinCollected>().SingleOrDefault();
        Assert.NotNull(evt);
        Assert.Equal(p1Piece.Id, evt.PieceId);
        Assert.Equal(CoinType.Silver, evt.CoinType);
        Assert.Equal(1, evt.Value);
    }

    // ── Test 9: Gold coin collected ───────────────────────────────────────────

    [Fact]
    public void Move_ThroughGoldCoin_IncreasesScoreByThree()
    {
        // Arrange: piece at (0,3), gold coin at (0,4)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithOnePieceEach();
        game.Board.GetTile(new Position(0, 4)).SetOccupant(new Coin(CoinType.Gold));

        // Act
        game.MovePiece(p1, p1Piece.Id, BuildSegments(new Position(0, 4)));

        // Assert
        Assert.Equal(3, game.Scores[p1]);
    }

    [Fact]
    public void Move_ThroughGoldCoin_RaisesCoinCollectedEventWithGoldType()
    {
        // Arrange
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithOnePieceEach();
        game.Board.GetTile(new Position(0, 4)).SetOccupant(new Coin(CoinType.Gold));

        // Act
        game.MovePiece(p1, p1Piece.Id, BuildSegments(new Position(0, 4)));

        // Assert
        var evt = game.DomainEvents.OfType<CoinCollected>().SingleOrDefault();
        Assert.NotNull(evt);
        Assert.Equal(CoinType.Gold, evt.CoinType);
        Assert.Equal(3, evt.Value);
    }

    // ── Test 10: MovesPerTurn=2 requires exactly 2 segments ──────────────────

    [Fact]
    public void MultiMovePiece_WithOnlyOneSegment_ThrowsDomainException()
    {
        // Arrange: piece with MovesPerTurn=2 at (0,3)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithOnePieceEach(
            p1MovesPerTurn: 2,
            p1MaxDistance: 2);

        // Act & Assert: submit only 1 segment when 2 are required
        Assert.Throws<DomainException>(() =>
            game.MovePiece(p1, p1Piece.Id, BuildSegments(new Position(0, 4))));
    }

    [Fact]
    public void MultiMovePiece_WithExactlyTwoSegments_Succeeds()
    {
        // Arrange: piece with MovesPerTurn=2 at (0,3)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithOnePieceEach(
            p1MovesPerTurn: 2,
            p1MaxDistance: 2);

        // Act: two segments, each with 1 step
        var segment1 = (IReadOnlyList<Position>)new List<Position> { new Position(0, 4) }.AsReadOnly();
        var segment2 = (IReadOnlyList<Position>)new List<Position> { new Position(0, 5) }.AsReadOnly();
        var segments = (IReadOnlyList<IReadOnlyList<Position>>)new List<IReadOnlyList<Position>> { segment1, segment2 }.AsReadOnly();

        game.MovePiece(p1, p1Piece.Id, segments); // should not throw

        Assert.Equal(new Position(0, 5), p1Piece.Position);
    }

    // ── Test 11: MaxDistance enforced ────────────────────────────────────────

    [Fact]
    public void Move_WithSegmentExceedingMaxDistance_ThrowsDomainException()
    {
        // Arrange: piece with maxDistance=2, attempt a 3-step segment
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithOnePieceEach(
            p1MaxDistance: 2);

        // Act & Assert: 3 steps in one segment exceed MaxDistance=2
        Assert.Throws<DomainException>(() =>
            game.MovePiece(p1, p1Piece.Id, BuildSegments(
                new Position(0, 4),
                new Position(0, 5),
                new Position(0, 6))));
    }

    // ── Test 12: Wrong phase guard ────────────────────────────────────────────

    [Fact]
    public void MovePiece_DuringPlacePhase_ThrowsDomainException()
    {
        // Arrange: game in PlacePhase
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
        game.AdvancePhase(); // CoinSpawn → PlacePhase (still PlacePhase)

        // Act & Assert: calling MovePiece during PlacePhase must throw
        Assert.Throws<DomainException>(() =>
            game.MovePiece(p1, Guid.NewGuid(), new List<IReadOnlyList<Position>>()));
    }

    [Fact]
    public void MovePiece_DuringCoinSpawnPhase_ThrowsDomainException()
    {
        // Arrange: game in CoinSpawn (right after Start)
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
        game.Start(); // → CoinSpawn

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            game.MovePiece(p1, Guid.NewGuid(), new List<IReadOnlyList<Position>>()));
    }

    // ── Test 13: Auto-advance when all on-board pieces have moved ─────────────

    [Fact]
    public void MovePiece_AutoAdvances_WhenAllPiecesMoved()
    {
        // Arrange: p1 piece at (0,3), p2 piece at (7,3)
        var (game, p1, p2, p1Piece, p2Piece) = GameInMovePhaseWithOnePieceEach();

        // Act: p1 moves their piece, then p2 moves theirs
        game.MovePiece(p1, p1Piece.Id, BuildSegments(new Position(0, 4)));
        game.MovePiece(p2, p2Piece.Id, BuildSegments(new Position(7, 4)));

        // Assert: phase advanced to CoinSpawn (turn advanced)
        Assert.Equal(TurnPhase.CoinSpawn, game.CurrentPhase);
    }

    [Fact]
    public void MovePiece_AutoAdvances_TurnNumberIncrements()
    {
        // Arrange
        var (game, p1, p2, p1Piece, p2Piece) = GameInMovePhaseWithOnePieceEach();

        // Act
        game.MovePiece(p1, p1Piece.Id, BuildSegments(new Position(0, 4)));
        game.MovePiece(p2, p2Piece.Id, BuildSegments(new Position(7, 4)));

        // Assert: turn 1 → turn 2
        Assert.Equal(2, game.TurnNumber);
    }

    [Fact]
    public void MovePiece_OnlyP1MovesPiece_PhaseRemainsInMovePhase()
    {
        // Arrange
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithOnePieceEach();

        // Act: only p1 moves their piece
        game.MovePiece(p1, p1Piece.Id, BuildSegments(new Position(0, 4)));

        // Assert: still in MovePhase (p2 hasn't moved yet)
        Assert.Equal(TurnPhase.MovePhase, game.CurrentPhase);
    }

    // ── Test 14: The same piece moved twice throws ────────────────────────────────

    [Fact]
    public void MovePiece_SamePieceTwice_ThrowsDomainException()
    {
        // Arrange: p1 piece at (0,3)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithOnePieceEach();

        // Act: p1 moves their piece once
        game.MovePiece(p1, p1Piece.Id, BuildSegments(new Position(0, 4)));

        // Assert: second call for the same piece throws
        Assert.Throws<DomainException>(() =>
            game.MovePiece(p1, p1Piece.Id, BuildSegments(new Position(0, 5))));
    }

    // ── Bonus: Non-participant player ─────────────────────────────────────────

    [Fact]
    public void MovePiece_UnknownPlayerId_ThrowsDomainException()
    {
        // Arrange: game in MovePhase
        var (game, _, _, _, _) = GameInMovePhaseWithOnePieceEach();
        var stranger = Guid.NewGuid();

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            game.MovePiece(stranger, Guid.NewGuid(), new List<IReadOnlyList<Position>>()));
    }

    // ── Bonus: Fence blocking (orthogonal) ────────────────────────────────────

    [Fact]
    public void Move_AcrossOrthogonalFencedEdge_ThrowsDomainException()
    {
        // Arrange: piece at (0,3); fence between (0,3) and (0,4) blocks right move
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithOnePieceEach();
        game.Board.AddFence(new Fence(new Position(0, 3), new Position(0, 4)));

        // Act & Assert: moving right is blocked by the fence
        Assert.Throws<DomainException>(() =>
            game.MovePiece(p1, p1Piece.Id, BuildSegments(new Position(0, 4))));
    }

    // ── Bonus: Fence blocking (diagonal through a corner) ───────────────────────

    [Fact]
    public void Move_DiagonallyThroughCornerFormedByTwoFences_ThrowsDomainException()
    {
        // Arrange: AnyDirection piece at (0,4)
        // Two fences form a corner at (0,4): fence (0,4)-(1,4) and fence (0,4)-(0,5)
        // This blocks the diagonal step (0,4)→(1,5).
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithOnePieceEach(
            p1MovementType: MovementType.AnyDirection,
            p1StartPos: new Position(0, 4));

        game.Board.AddFence(new Fence(new Position(0, 4), new Position(1, 4)));
        game.Board.AddFence(new Fence(new Position(0, 4), new Position(0, 5)));

        // Act & Assert: diagonal step blocked by corner fences
        Assert.Throws<DomainException>(() =>
            game.MovePiece(p1, p1Piece.Id, BuildSegments(new Position(1, 5))));
    }

    // ── Bonus: Path collection (multiple coins) ───────────────────────────────

    [Fact]
    public void Move_ThroughMultipleCoins_CollectsAllAndAccumulatesScore()
    {
        // Arrange: piece at (0,3); two silver coins at (0,4) and (0,5)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithOnePieceEach(p1MaxDistance: 3);
        game.Board.GetTile(new Position(0, 4)).SetOccupant(new Coin(CoinType.Silver));
        game.Board.GetTile(new Position(0, 5)).SetOccupant(new Coin(CoinType.Silver));

        // Act: move through both coin tiles
        game.MovePiece(p1, p1Piece.Id, BuildSegments(
            new Position(0, 4),
            new Position(0, 5),
            new Position(0, 6)));

        // Assert: score = 2 (1+1)
        Assert.Equal(2, game.Scores[p1]);
    }

    [Fact]
    public void Move_ThroughMultipleCoins_RaisesCoinCollectedEventPerCoin()
    {
        // Arrange
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithOnePieceEach(p1MaxDistance: 3);
        game.Board.GetTile(new Position(0, 4)).SetOccupant(new Coin(CoinType.Silver));
        game.Board.GetTile(new Position(0, 5)).SetOccupant(new Coin(CoinType.Gold));

        // Act
        game.MovePiece(p1, p1Piece.Id, BuildSegments(
            new Position(0, 4),
            new Position(0, 5),
            new Position(0, 6)));

        // Assert: 2 CoinCollected events (one per coin)
        var coinEvents = game.DomainEvents.OfType<CoinCollected>().ToList();
        Assert.Equal(2, coinEvents.Count);
    }

    // ── Bonus: MaxDistance = 1 (minimum valid distance) ──────────────────────

    [Fact]
    public void Move_WithExactlyMaxDistanceSteps_Succeeds()
    {
        // Arrange: piece with maxDistance=1 at (0,3)
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithOnePieceEach(
            p1MaxDistance: 1);

        // Act: 1 step (exactly MaxDistance)
        game.MovePiece(p1, p1Piece.Id, BuildSegments(new Position(0, 4))); // should not throw

        Assert.Equal(new Position(0, 4), p1Piece.Position);
    }

    // ── Sequential move order ─────────────────────────────────────────────────

    [Fact]
    public void MovePiece_PlayerTwoBeforePlayerOneDone_ThrowsDomainException()
    {
        // Arrange: both players have one piece on board
        var (game, _, p2, _, p2Piece) = GameInMovePhaseWithOnePieceEach();

        // Act: P2 tries to move before P1 has moved any piece
        var ex = Assert.Throws<DomainException>(() =>
            game.MovePiece(p2, p2Piece.Id, BuildSegments(new Position(7, 4))));

        Assert.Contains("not", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MovePiece_PlayerTwoCanMoveAfterPlayerOneDone()
    {
        // Arrange
        var (game, p1, p2, p1Piece, p2Piece) = GameInMovePhaseWithOnePieceEach();

        // Act: P1 moves first
        game.MovePiece(p1, p1Piece.Id, BuildSegments(new Position(0, 4)));

        // Now P2 can move
        game.MovePiece(p2, p2Piece.Id, BuildSegments(new Position(7, 4)));

        // Assert: both pieces moved
        Assert.Equal(new Position(0, 4), p1Piece.Position);
        Assert.Equal(new Position(7, 4), p2Piece.Position);
    }

    [Fact]
    public void MovePiece_PlayerOneWithNoPieces_PlayerTwoCanMoveImmediately()
    {
        // Arrange: only P2 has a piece on board
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var board = new Board();

        var p1Pieces = Enumerable.Range(0, 5)
            .Select(i => new Piece(Guid.NewGuid(), $"P1Piece{i}", p1, EntryPointType.Borders, MovementType.Orthogonal, 3, 1))
            .ToList();
        var p2Piece = new Piece(Guid.NewGuid(), "P2Piece", p2, EntryPointType.Borders, MovementType.Orthogonal, 3, 1);
        var p2Pieces = new List<Piece> { p2Piece }
            .Concat(Enumerable.Range(1, 4).Select(i => new Piece(Guid.NewGuid(), $"P2Extra{i}", p2, EntryPointType.Borders, MovementType.Orthogonal, 1, 1)))
            .ToList();

        var game = new Game(p1, p2, board);
        game.SetLineup(p1, new Lineup(p1Pieces));
        game.SetLineup(p2, new Lineup(p2Pieces));
        game.Start();
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        // Only place P2's piece
        p2Piece.PlaceAt(new Position(7, 0));
        board.GetTile(new Position(7, 0)).SetOccupant(p2Piece);

        game.AdvancePhase(); // PlacePhase → MovePhase

        // Act: P2 moves — P1 is lazily skipped (no pieces)
        game.MovePiece(p2, p2Piece.Id, BuildSegments(new Position(7, 1)));

        // Assert: phase auto-advanced (both done)
        Assert.NotEqual(TurnPhase.MovePhase, game.CurrentPhase);
        Assert.Equal(new Position(7, 1), p2Piece.Position);
    }
}
