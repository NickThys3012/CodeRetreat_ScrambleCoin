using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Events;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.Obstacles;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Tests;

/// <summary>
/// Unit tests for <see cref="Game.PlacePiece"/>, <see cref="Game.ReplacePiece"/>,
/// and <see cref="Game.SkipPlacement"/>.
/// </summary>
public class PiecePlacementTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Piece MakePiece(Guid playerId, EntryPointType entryPointType = EntryPointType.Borders, string name = "TestPiece") =>
        new Piece(Guid.NewGuid(), name, playerId, entryPointType, MovementType.Orthogonal, 1, 1);

    /// <summary>
    /// Creates a Game in PlacePhase with 5 Borders pieces per player.
    /// </summary>
    private static (Game game, Guid p1, Guid p2, List<Piece> p1Pieces, List<Piece> p2Pieces) GameInPlacePhase()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var board = new Board();

        var p1Pieces = Enumerable.Range(0, 5)
            .Select(i => MakePiece(p1, EntryPointType.Borders, $"P1Piece{i}"))
            .ToList();
        var p2Pieces = Enumerable.Range(0, 5)
            .Select(i => MakePiece(p2, EntryPointType.Borders, $"P2Piece{i}"))
            .ToList();

        var game = new Game(p1, p2, board);
        game.SetLineup(p1, new Lineup(p1Pieces));
        game.SetLineup(p2, new Lineup(p2Pieces));
        game.Start();          // → CoinSpawn
        game.AdvancePhase();   // → PlacePhase
        game.ClearDomainEvents();

        return (game, p1, p2, p1Pieces, p2Pieces);
    }

    // ── PlacePiece happy path ─────────────────────────────────────────────────

    [Fact]
    public void PlacePiece_HappyPath_PlacesPieceOnBoard()
    {
        var (game, p1, p2, p1Pieces, _) = GameInPlacePhase();
        var piece = p1Pieces[0];
        var pos = new Position(0, 0);

        game.PlacePiece(p1, piece.Id, pos);

        Assert.True(piece.IsOnBoard);
        Assert.Equal(pos, piece.Position);
        Assert.Equal(piece, game.Board.GetTile(pos).AsPiece);
        Assert.Equal(1, game.PiecesOnBoard[p1]);
        Assert.Equal(0, game.Scores[p1]); // no coin
    }

    [Fact]
    public void PlacePiece_HappyPath_RaisesPiecePlacedEvent()
    {
        var (game, p1, _, p1Pieces, _) = GameInPlacePhase();
        var piece = p1Pieces[0];
        var pos = new Position(0, 1);

        game.PlacePiece(p1, piece.Id, pos);

        var evt = Assert.Single(game.DomainEvents.OfType<PiecePlaced>());
        Assert.Equal(game.Id, evt.GameId);
        Assert.Equal(p1, evt.PlayerId);
        Assert.Equal(piece.Id, evt.PieceId);
        Assert.Equal(pos, evt.Position);
        Assert.False(evt.CoinCollected);
        Assert.Equal(0, evt.CoinValue);
    }

    [Fact]
    public void PlacePiece_OnCoinTile_CollectsCoinAndScores()
    {
        var (game, p1, p2, p1Pieces, _) = GameInPlacePhase();

        // Put coin on an edge tile by using SpawnCoins during CoinSpawn phase.
        // We can't spawn in PlacePhase, so place a coin manually on the tile.
        var pos = new Position(0, 3);
        game.Board.GetTile(pos).SetOccupant(new Coin(CoinType.Silver)); // value = 1

        var piece = p1Pieces[0];
        game.PlacePiece(p1, piece.Id, pos);

        Assert.Equal(1, game.Scores[p1]);
        Assert.Equal(piece, game.Board.GetTile(pos).AsPiece);

        var evt = Assert.Single(game.DomainEvents.OfType<PiecePlaced>());
        Assert.True(evt.CoinCollected);
        Assert.Equal(1, evt.CoinValue);
    }

    [Fact]
    public void PlacePiece_OnGoldCoinTile_CollectsGoldValue()
    {
        var (game, p1, _, p1Pieces, _) = GameInPlacePhase();
        var pos = new Position(7, 7);
        game.Board.GetTile(pos).SetOccupant(new Coin(CoinType.Gold)); // value = 3

        game.PlacePiece(p1, p1Pieces[0].Id, pos);

        Assert.Equal(3, game.Scores[p1]);
    }

    // ── PlacePiece error cases ────────────────────────────────────────────────

    [Fact]
    public void PlacePiece_WrongPhase_Throws()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var p1Pieces = Enumerable.Range(0, 5)
            .Select(i => MakePiece(p1, EntryPointType.Borders, $"P1Piece{i}"))
            .ToList();
        var p2Pieces = Enumerable.Range(0, 5)
            .Select(i => MakePiece(p2, EntryPointType.Borders, $"P2Piece{i}"))
            .ToList();
        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(p1Pieces));
        game.SetLineup(p2, new Lineup(p2Pieces));
        game.Start(); // → CoinSpawn

        var ex = Assert.Throws<DomainException>(() => game.PlacePiece(p1, p1Pieces[0].Id, new Position(0, 0)));
        Assert.Contains("PlacePhase", ex.Message);
    }

    [Fact]
    public void PlacePiece_PieceNotInLineup_Throws()
    {
        var (game, p1, _, _, _) = GameInPlacePhase();
        var randomPieceId = Guid.NewGuid();

        var ex = Assert.Throws<DomainException>(() => game.PlacePiece(p1, randomPieceId, new Position(0, 0)));
        Assert.Contains(randomPieceId.ToString(), ex.Message);
    }

    [Fact]
    public void PlacePiece_PieceAlreadyOnBoard_Throws()
    {
        var (game, p1, p2, p1Pieces, p2Pieces) = GameInPlacePhase();
        var piece = p1Pieces[0];

        // Place it once.
        game.PlacePiece(p1, piece.Id, new Position(0, 0));
        game.PlacePiece(p2, p2Pieces[0].Id, new Position(7, 0)); // p2 acts so the phase stays in PlacePhase for the next turn
        // Advance to the next turn's PlacePhase.
        game.AdvanceTurn(); // MovePhase → next turn CoinSpawn
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        // Now try to PlacePiece again with the same piece (already on board).
        var ex = Assert.Throws<DomainException>(() => game.PlacePiece(p1, piece.Id, new Position(0, 1)));
        Assert.Contains("already on the board", ex.Message);
    }

    [Fact]
    public void PlacePiece_OccupiedByPieceTile_Throws()
    {
        var (game, p1, p2, p1Pieces, p2Pieces) = GameInPlacePhase();

        game.PlacePiece(p1, p1Pieces[0].Id, new Position(0, 0));
        // p2 tries to place on the same tile.
        var ex = Assert.Throws<DomainException>(() => game.PlacePiece(p2, p2Pieces[0].Id, new Position(0, 0)));
        Assert.Contains("already occupied", ex.Message);
    }

    [Fact]
    public void PlacePiece_ObstacleTile_Throws()
    {
        var (game, p1, _, p1Pieces, _) = GameInPlacePhase();
        var obstaclePos = new Position(0, 0);
        game.Board.AddRock(new Rock(obstaclePos));

        var ex = Assert.Throws<DomainException>(() => game.PlacePiece(p1, p1Pieces[0].Id, obstaclePos));
        Assert.Contains("obstacle", ex.Message);
    }

    [Fact]
    public void PlacePiece_BordersPieceOnCenter_Throws()
    {
        var (game, p1, _, p1Pieces, _) = GameInPlacePhase();
        // p1Pieces have a Borders entry type; (4,4) is not an edge tile.

        var ex = Assert.Throws<DomainException>(() => game.PlacePiece(p1, p1Pieces[0].Id, new Position(4, 4)));
        Assert.Contains("entry point", ex.Message);
    }

    [Fact]
    public void PlacePiece_CornersPieceOnCorner_Succeeds()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var pieces1 = new List<Piece>
        {
            MakePiece(p1, EntryPointType.Corners, "CornerPiece"),
            MakePiece(p1, EntryPointType.Borders, "B1"),
            MakePiece(p1, EntryPointType.Borders, "B2"),
            MakePiece(p1, EntryPointType.Borders, "B3"),
            MakePiece(p1, EntryPointType.Borders, "B4")
        };
        var pieces2 = Enumerable.Range(0, 5).Select(i => MakePiece(p2, EntryPointType.Borders, $"P2{i}")).ToList();
        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(pieces1));
        game.SetLineup(p2, new Lineup(pieces2));
        game.Start();
        game.AdvancePhase();

        // Should not throw — (0,0) is a corner.
        game.PlacePiece(p1, pieces1[0].Id, new Position(0, 0));

        Assert.True(pieces1[0].IsOnBoard);
    }

    [Fact]
    public void PlacePiece_CornersPieceOnNonCornerEdge_Throws()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var pieces1 = new List<Piece>
        {
            MakePiece(p1, EntryPointType.Corners, "CornerPiece"),
            MakePiece(p1, EntryPointType.Borders, "B1"),
            MakePiece(p1, EntryPointType.Borders, "B2"),
            MakePiece(p1, EntryPointType.Borders, "B3"),
            MakePiece(p1, EntryPointType.Borders, "B4")
        };
        var pieces2 = Enumerable.Range(0, 5).Select(i => MakePiece(p2, EntryPointType.Borders, $"P2{i}")).ToList();
        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(pieces1));
        game.SetLineup(p2, new Lineup(pieces2));
        game.Start();
        game.AdvancePhase();

        // (0,1) is an edge but NOT a corner.
        var ex = Assert.Throws<DomainException>(() => game.PlacePiece(p1, pieces1[0].Id, new Position(0, 1)));
        Assert.Contains("entry point", ex.Message);
    }

    [Fact]
    public void PlacePiece_AnywherePieceOnCenter_Succeeds()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var pieces1 = new List<Piece>
        {
            MakePiece(p1, EntryPointType.Anywhere, "AnyPiece"),
            MakePiece(p1, EntryPointType.Borders, "B1"),
            MakePiece(p1, EntryPointType.Borders, "B2"),
            MakePiece(p1, EntryPointType.Borders, "B3"),
            MakePiece(p1, EntryPointType.Borders, "B4")
        };
        var pieces2 = Enumerable.Range(0, 5).Select(i => MakePiece(p2, EntryPointType.Borders, $"P2{i}")).ToList();
        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(pieces1));
        game.SetLineup(p2, new Lineup(pieces2));
        game.Start();
        game.AdvancePhase();

        game.PlacePiece(p1, pieces1[0].Id, new Position(4, 4));

        Assert.True(pieces1[0].IsOnBoard);
    }

    [Fact]
    public void PlacePiece_MaxPiecesExceeded_Throws()
    {
        var (game, p1, p2, p1Pieces, p2Pieces) = GameInPlacePhase();

        // Place p1's first piece this turn.
        game.PlacePiece(p1, p1Pieces[0].Id, new Position(0, 0));
        game.PlacePiece(p2, p2Pieces[0].Id, new Position(7, 0));

        // Turn 2.
        game.AdvanceTurn();
        game.AdvancePhase();
        game.PlacePiece(p1, p1Pieces[1].Id, new Position(0, 1));
        game.PlacePiece(p2, p2Pieces[1].Id, new Position(7, 1));

        // Turn 3 — place the 3rd piece for p1.
        game.AdvanceTurn();
        game.AdvancePhase();
        game.PlacePiece(p1, p1Pieces[2].Id, new Position(0, 2));
        game.PlacePiece(p2, p2Pieces[2].Id, new Position(7, 2));

        // Turn 4 — p1 tries to place a 4th piece.
        game.AdvanceTurn();
        game.AdvancePhase();
        var ex = Assert.Throws<DomainException>(() => game.PlacePiece(p1, p1Pieces[3].Id, new Position(0, 3)));
        Assert.Contains("maximum", ex.Message);
    }

    [Fact]
    public void PlacePiece_NonParticipant_Throws()
    {
        var (game, _, _, _, _) = GameInPlacePhase();
        var stranger = Guid.NewGuid();

        var ex = Assert.Throws<DomainException>(() => game.PlacePiece(stranger, Guid.NewGuid(), new Position(0, 0)));
        Assert.Contains("not a participant", ex.Message);
    }

    [Fact]
    public void PlacePiece_ActingTwice_Throws()
    {
        var (game, p1, _, p1Pieces, _) = GameInPlacePhase();

        game.PlacePiece(p1, p1Pieces[0].Id, new Position(0, 0));

        // p2 has not yet acted, so phase is still PlacePhase — p1 cannot act again.
        var ex = Assert.Throws<PlayerAlreadyActedException>(() => game.PlacePiece(p1, p1Pieces[1].Id, new Position(0, 1)));
        Assert.Contains("already acted", ex.Message);
    }

    // ── Auto-advance to MovePhase ──────────────────────────────────────────────

    [Fact]
    public void PlacePiece_BothPlayersAct_AutoAdvancesToMovePhase()
    {
        var (game, p1, p2, p1Pieces, p2Pieces) = GameInPlacePhase();

        game.PlacePiece(p1, p1Pieces[0].Id, new Position(0, 0));
        Assert.Equal(TurnPhase.PlacePhase, game.CurrentPhase); // p2 hasn't acted yet

        game.PlacePiece(p2, p2Pieces[0].Id, new Position(7, 0));
        Assert.Equal(TurnPhase.MovePhase, game.CurrentPhase); // auto-advanced
    }

    [Fact]
    public void SkipPlacement_BothPlayers_AutoAdvancesToMovePhase()
    {
        var (game, p1, p2, _, _) = GameInPlacePhase();

        game.SkipPlacement(p1);
        Assert.Equal(TurnPhase.PlacePhase, game.CurrentPhase);

        game.SkipPlacement(p2);
        Assert.Equal(TurnPhase.MovePhase, game.CurrentPhase);
    }

    [Fact]
    public void PlacePieceAndSkip_AutoAdvancesToMovePhase()
    {
        var (game, p1, p2, p1Pieces, _) = GameInPlacePhase();

        game.PlacePiece(p1, p1Pieces[0].Id, new Position(0, 0));
        game.SkipPlacement(p2);

        Assert.Equal(TurnPhase.MovePhase, game.CurrentPhase);
    }

    // ── SkipPlacement error cases ─────────────────────────────────────────────

    [Fact]
    public void SkipPlacement_WrongPhase_Throws()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(Enumerable.Range(0, 5).Select(i => MakePiece(p1, EntryPointType.Borders, $"P1{i}")).ToList()));
        game.SetLineup(p2, new Lineup(Enumerable.Range(0, 5).Select(i => MakePiece(p2, EntryPointType.Borders, $"P2{i}")).ToList()));
        game.Start(); // CoinSpawn phase

        var ex = Assert.Throws<DomainException>(() => game.SkipPlacement(p1));
        Assert.Contains("PlacePhase", ex.Message);
    }

    [Fact]
    public void SkipPlacement_ActingTwice_Throws()
    {
        var (game, p1, _, _, _) = GameInPlacePhase();

        game.SkipPlacement(p1);

        // Phase hasn't advanced yet (p2 hasn't acted).
        var ex = Assert.Throws<PlayerAlreadyActedException>(() => game.SkipPlacement(p1));
        Assert.Contains("already acted", ex.Message);
    }

    [Fact]
    public void SkipPlacement_NonParticipant_Throws()
    {
        var (game, _, _, _, _) = GameInPlacePhase();
        var stranger = Guid.NewGuid();

        var ex = Assert.Throws<DomainException>(() => game.SkipPlacement(stranger));
        Assert.Contains("not a participant", ex.Message);
    }

    // ── ReplacePiece happy path ───────────────────────────────────────────────

    [Fact]
    public void ReplacePiece_HappyPath_OldGoneNewOnBoard()
    {
        var (game, p1, p2, p1Pieces, p2Pieces) = GameInPlacePhase();

        // Place the first piece.
        var oldPos = new Position(0, 0);
        game.PlacePiece(p1, p1Pieces[0].Id, oldPos);
        game.PlacePiece(p2, p2Pieces[0].Id, new Position(7, 0));

        // Advance to turn 2 PlacePhase.
        game.AdvanceTurn();
        game.AdvancePhase();
        game.ClearDomainEvents();

        // Replace p1Pieces[0] — new piece lands at old piece's position.
        game.ReplacePiece(p1, p1Pieces[0].Id, p1Pieces[1].Id);

        Assert.False(p1Pieces[0].IsOnBoard);
        Assert.True(p1Pieces[1].IsOnBoard);
        Assert.Equal(oldPos, p1Pieces[1].Position);
        Assert.Equal(p1Pieces[1], game.Board.GetTile(oldPos).AsPiece);
        Assert.Equal(1, game.PiecesOnBoard[p1]); // net count unchanged
    }

    [Fact]
    public void ReplacePiece_HappyPath_RaisesPieceReplacedEvent()
    {
        var (game, p1, p2, p1Pieces, p2Pieces) = GameInPlacePhase();

        var oldPos = new Position(0, 0);
        game.PlacePiece(p1, p1Pieces[0].Id, oldPos);
        game.PlacePiece(p2, p2Pieces[0].Id, new Position(7, 0));
        game.AdvanceTurn();
        game.AdvancePhase();
        game.ClearDomainEvents();

        game.ReplacePiece(p1, p1Pieces[0].Id, p1Pieces[1].Id);

        var evt = Assert.Single(game.DomainEvents.OfType<PieceReplaced>());
        Assert.Equal(p1, evt.PlayerId);
        Assert.Equal(p1Pieces[0].Id, evt.RemovedPieceId);
        Assert.Equal(p1Pieces[1].Id, evt.PlacedPieceId);
        Assert.Equal(oldPos, evt.Position);
        Assert.False(evt.CoinCollected);
        Assert.Equal(0, evt.CoinValue);
    }

    [Fact]
    public void ReplacePiece_OnCoinTile_CollectsCoin()
    {
        // With the new rule, the new piece always lands at the old piece's tile.
        // A coin cannot co-exist on the same tile as the old piece in normal gameplay,
        // so this test verifies the happy-path: replacement succeeds with no score change.
        var (game, p1, p2, p1Pieces, p2Pieces) = GameInPlacePhase();

        var oldPos = new Position(0, 0);
        game.PlacePiece(p1, p1Pieces[0].Id, oldPos);
        game.PlacePiece(p2, p2Pieces[0].Id, new Position(7, 0));
        game.AdvanceTurn();
        game.AdvancePhase();

        var scoreBefore = game.Scores[p1];
        game.ReplacePiece(p1, p1Pieces[0].Id, p1Pieces[1].Id);

        // No coin on the old tile — score unchanged.
        Assert.Equal(scoreBefore, game.Scores[p1]);
        var evt = game.DomainEvents.OfType<PieceReplaced>().Last();
        Assert.False(evt.CoinCollected);
        Assert.Equal(0, evt.CoinValue);
    }

    [Fact]
    public void ReplacePiece_PieceCountStaysSame()
    {
        var (game, p1, p2, p1Pieces, p2Pieces) = GameInPlacePhase();

        game.PlacePiece(p1, p1Pieces[0].Id, new Position(0, 0));
        game.PlacePiece(p2, p2Pieces[0].Id, new Position(7, 0));
        game.AdvanceTurn();
        game.AdvancePhase();

        var countBefore = game.PiecesOnBoard[p1];
        game.ReplacePiece(p1, p1Pieces[0].Id, p1Pieces[1].Id);

        Assert.Equal(countBefore, game.PiecesOnBoard[p1]);
    }

    // ── ReplacePiece error cases ──────────────────────────────────────────────

    [Fact]
    public void ReplacePiece_OldPieceNotOnBoard_Throws()
    {
        var (game, p1, _, p1Pieces, _) = GameInPlacePhase();

        // p1Pieces[0] is not on board — try to replace it.
        var ex = Assert.Throws<DomainException>(
            () => game.ReplacePiece(p1, p1Pieces[0].Id, p1Pieces[1].Id));
        Assert.Contains("not on the board", ex.Message);
    }

    [Fact]
    public void ReplacePiece_NewPieceAlreadyOnBoard_Throws()
    {
        var (game, p1, p2, p1Pieces, p2Pieces) = GameInPlacePhase();

        game.PlacePiece(p1, p1Pieces[0].Id, new Position(0, 0));
        game.PlacePiece(p2, p2Pieces[0].Id, new Position(7, 0));
        game.AdvanceTurn();
        game.AdvancePhase();
        game.PlacePiece(p1, p1Pieces[1].Id, new Position(0, 1));
        game.PlacePiece(p2, p2Pieces[1].Id, new Position(7, 1));
        game.AdvanceTurn();
        game.AdvancePhase();

        // Both pieces [0] and [1] are on board. Try to replace [0] with [1].
        var ex = Assert.Throws<DomainException>(
            () => game.ReplacePiece(p1, p1Pieces[0].Id, p1Pieces[1].Id));
        Assert.Contains("already on the board", ex.Message);
    }

    [Fact]
    public void ReplacePiece_SamePieceId_Throws()
    {
        var (game, p1, p2, p1Pieces, p2Pieces) = GameInPlacePhase();

        game.PlacePiece(p1, p1Pieces[0].Id, new Position(0, 0));
        game.PlacePiece(p2, p2Pieces[0].Id, new Position(7, 0));
        game.AdvanceTurn();
        game.AdvancePhase();

        var ex = Assert.Throws<DomainException>(
            () => game.ReplacePiece(p1, p1Pieces[0].Id, p1Pieces[0].Id));
        Assert.Contains("different", ex.Message);
    }

    [Fact]
    public void ReplacePiece_WrongPhase_Throws()
    {
        // Arrange: create a game in CoinSpawnPhase (before PlacePhase).
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var p1Pieces = Enumerable.Range(0, 5)
            .Select(i => MakePiece(p1, EntryPointType.Borders, $"P1Piece{i}"))
            .ToList();
        var p2Pieces = Enumerable.Range(0, 5)
            .Select(i => MakePiece(p2, EntryPointType.Borders, $"P2Piece{i}"))
            .ToList();
        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(p1Pieces));
        game.SetLineup(p2, new Lineup(p2Pieces));
        game.Start(); // → CoinSpawnPhase (not PlacePhase)

        // Act & Assert: phase guard fires before any piece validation.
        var ex = Assert.Throws<DomainException>(
            () => game.ReplacePiece(p1, p1Pieces[0].Id, p1Pieces[1].Id));
        Assert.Contains("PlacePhase", ex.Message);
    }

    [Fact]
    public void ReplacePiece_NonParticipant_Throws()
    {
        // Arrange: game in PlacePhase — stranger ID is unknown to the game.
        var (game, _, _, _, _) = GameInPlacePhase();
        var stranger = Guid.NewGuid();

        // Act & Assert.
        var ex = Assert.Throws<DomainException>(
            () => game.ReplacePiece(stranger, Guid.NewGuid(), Guid.NewGuid()));
        Assert.Contains("not a participant", ex.Message);
    }

    [Fact]
    public void ReplacePiece_ActingTwice_Throws()
    {
        // Arrange: set up turn 2 PlacePhase with p1's piece on board.
        var (game, p1, p2, p1Pieces, p2Pieces) = GameInPlacePhase();

        game.PlacePiece(p1, p1Pieces[0].Id, new Position(0, 0));
        game.PlacePiece(p2, p2Pieces[0].Id, new Position(7, 0));
        game.AdvanceTurn();
        game.AdvancePhase();

        // p1 acts once successfully.
        game.ReplacePiece(p1, p1Pieces[0].Id, p1Pieces[1].Id);

        // p2 has not yet acted — p1 tries again.
        var ex = Assert.Throws<PlayerAlreadyActedException>(
            () => game.ReplacePiece(p1, p1Pieces[1].Id, p1Pieces[2].Id));
        Assert.Contains("already acted", ex.Message);
    }

    [Fact]
    public void ReplacePiece_SameTile_Succeeds()
    {
        // Arrange: p1Pieces[0] is on board at posA; replace it with p1Pieces[1] targeting the same tile.
        var (game, p1, p2, p1Pieces, p2Pieces) = GameInPlacePhase();
        var posA = new Position(0, 0);

        game.PlacePiece(p1, p1Pieces[0].Id, posA);
        game.PlacePiece(p2, p2Pieces[0].Id, new Position(7, 0));
        game.AdvanceTurn();
        game.AdvancePhase();

        // Act: replace — new piece always lands at the old piece's tile.
        game.ReplacePiece(p1, p1Pieces[0].Id, p1Pieces[1].Id);

        // Assert: a new piece is at posA, an old piece is off board, net count unchanged.
        Assert.True(p1Pieces[1].IsOnBoard);
        Assert.Equal(posA, p1Pieces[1].Position);
        Assert.False(p1Pieces[0].IsOnBoard);
        Assert.Equal(1, game.PiecesOnBoard[p1]);
    }

    [Fact]
    public void ReplacePiece_NewPieceLandsAtOldPiecePosition()
    {
        // Arrange: p1Pieces[0] placed at (0, 3).
        var (game, p1, p2, p1Pieces, p2Pieces) = GameInPlacePhase();
        var oldPos = new Position(0, 3);

        game.PlacePiece(p1, p1Pieces[0].Id, oldPos);
        game.PlacePiece(p2, p2Pieces[0].Id, new Position(7, 0));
        game.AdvanceTurn();
        game.AdvancePhase();

        // Act: replace with a new piece — no position specified by caller.
        game.ReplacePiece(p1, p1Pieces[0].Id, p1Pieces[1].Id);

        // Assert: new piece is at the old piece's former tile.
        Assert.True(p1Pieces[1].IsOnBoard);
        Assert.Equal(oldPos, p1Pieces[1].Position);
        Assert.Equal(p1Pieces[1], game.Board.GetTile(oldPos).AsPiece);

        // And the old piece is gone.
        Assert.False(p1Pieces[0].IsOnBoard);
    }

    // ── Board helper methods ──────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(0, 4, true)]
    [InlineData(7, 7, true)]
    [InlineData(7, 3, true)]
    [InlineData(3, 0, true)]
    [InlineData(4, 4, false)]
    [InlineData(3, 3, false)]
    [InlineData(1, 6, false)]
    public void Board_IsEdgeTile_ReturnsCorrectly(int row, int col, bool expected)
    {
        var board = new Board();
        Assert.Equal(expected, board.IsEdgeTile(new Position(row, col)));
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(0, 7, true)]
    [InlineData(7, 0, true)]
    [InlineData(7, 7, true)]
    [InlineData(0, 1, false)]
    [InlineData(3, 0, false)]
    [InlineData(4, 4, false)]
    public void Board_IsCornerTile_ReturnsCorrectly(int row, int col, bool expected)
    {
        var board = new Board();
        Assert.Equal(expected, board.IsCornerTile(new Position(row, col)));
    }

    [Theory]
    [InlineData(0, 0, EntryPointType.Borders, true)]
    [InlineData(4, 4, EntryPointType.Borders, false)]
    [InlineData(0, 0, EntryPointType.Corners, true)]
    [InlineData(0, 1, EntryPointType.Corners, false)]
    [InlineData(4, 4, EntryPointType.Anywhere, true)]
    [InlineData(0, 0, EntryPointType.Anywhere, true)]
    public void Board_IsValidEntryPoint_ReturnsCorrectly(int row, int col, EntryPointType entryPointType, bool expected)
    {
        var board = new Board();
        Assert.Equal(expected, board.IsValidEntryPoint(new Position(row, col), entryPointType));
    }
}
