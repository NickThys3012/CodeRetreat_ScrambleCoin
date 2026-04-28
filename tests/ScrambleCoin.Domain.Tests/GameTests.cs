using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Events;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.Tests.Helpers;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Tests;

public class GameTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Board NewBoard() => new Board();

    private static Lineup NewLineup() =>
        new Lineup(Enumerable.Range(0, 5).Select(i => PieceFactory.Any($"Piece{i}")).ToList());

    /// <summary>
    /// Creates a Game in WaitingForBots state with two distinct, random player IDs.
    /// </summary>
    private static (Game game, Guid p1, Guid p2) NewGame()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        return (new Game(p1, p2, NewBoard()), p1, p2);
    }

    /// <summary>
    /// Creates a Game that has had both lineups set and Start() called — status = InProgress.
    /// </summary>
    private static (Game game, Guid p1, Guid p2) StartedGame()
    {
        var (game, p1, p2) = NewGame();
        game.SetLineup(p1, NewLineup());
        game.SetLineup(p2, NewLineup());
        game.Start();
        return (game, p1, p2);
    }

    /// <summary>
    /// Advances the game through CoinSpawn and PlacePhase so it is in MovePhase,
    /// ready for a call to <see cref="Game.AdvanceTurn"/>.
    /// </summary>
    private static void AdvanceToMovePhase(Game game)
    {
        game.AdvancePhase(); // CoinSpawn → PlacePhase
        game.AdvancePhase(); // PlacePhase → MovePhase
    }


    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Constructor_DefaultStatus_IsWaitingForBots()
    {
        var (game, _, _) = NewGame();
        Assert.Equal(GameStatus.WaitingForBots, game.Status);
    }

    [Fact]
    public void Constructor_DefaultTurnNumber_IsZero()
    {
        var (game, _, _) = NewGame();
        Assert.Equal(0, game.TurnNumber);
    }

    [Fact]
    public void Constructor_PlayerOneScore_StartsAtZero()
    {
        var (game, p1, _) = NewGame();
        Assert.Equal(0, game.Scores[p1]);
    }

    [Fact]
    public void Constructor_PlayerTwoScore_StartsAtZero()
    {
        var (game, _, p2) = NewGame();
        Assert.Equal(0, game.Scores[p2]);
    }

    [Fact]
    public void Constructor_PlayerOnePiecesOnBoard_StartsAtZero()
    {
        var (game, p1, _) = NewGame();
        Assert.Equal(0, game.PiecesOnBoard[p1]);
    }

    [Fact]
    public void Constructor_PlayerTwoPiecesOnBoard_StartsAtZero()
    {
        var (game, _, p2) = NewGame();
        Assert.Equal(0, game.PiecesOnBoard[p2]);
    }

    [Fact]
    public void Constructor_WithSamePlayerOneAndPlayerTwo_ThrowsDomainException()
    {
        var id = Guid.NewGuid();
        Assert.Throws<DomainException>(() => new Game(id, id, NewBoard()));
    }

    [Fact]
    public void Constructor_WithNullBoard_ThrowsDomainException()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        Assert.Throws<DomainException>(() => new Game(p1, p2, null!));
    }

    [Fact]
    public void Constructor_AssignsIdCorrectly()
    {
        var gameId = Guid.NewGuid();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = new Game(gameId, p1, p2, NewBoard());
        Assert.Equal(gameId, game.Id);
    }

    [Fact]
    public void Constructor_BoardProperty_IsSameInstance()
    {
        var board = NewBoard();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = new Game(p1, p2, board);
        Assert.Same(board, game.Board);
    }

    [Fact]
    public void Constructor_ConvenienceOverload_AssignsNewId()
    {
        var (game, _, _) = NewGame();
        Assert.NotEqual(Guid.Empty, game.Id);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 2. SetLineup()
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SetLineup_ForPlayerOne_SetsLineupPlayerOne()
    {
        var (game, p1, _) = NewGame();
        var lineup = NewLineup();
        game.SetLineup(p1, lineup);
        Assert.Same(lineup, game.LineupPlayerOne);
    }

    [Fact]
    public void SetLineup_ForPlayerTwo_SetsLineupPlayerTwo()
    {
        var (game, _, p2) = NewGame();
        var lineup = NewLineup();
        game.SetLineup(p2, lineup);
        Assert.Same(lineup, game.LineupPlayerTwo);
    }

    [Fact]
    public void SetLineup_WhenGameIsInProgress_ThrowsDomainException()
    {
        var (game, p1, p2) = NewGame();
        game.SetLineup(p1, NewLineup());
        game.SetLineup(p2, NewLineup());
        game.Start();

        Assert.Throws<DomainException>(() => game.SetLineup(p1, NewLineup()));
    }

    [Fact]
    public void SetLineup_ForUnknownPlayer_ThrowsDomainException()
    {
        var (game, _, _) = NewGame();
        var stranger = Guid.NewGuid();
        Assert.Throws<DomainException>(() => game.SetLineup(stranger, NewLineup()));
    }

    [Fact]
    public void SetLineup_PlayerOneLineupAlreadySet_ThrowsDomainException()
    {
        var (game, p1, _) = NewGame();
        game.SetLineup(p1, NewLineup());
        Assert.Throws<DomainException>(() => game.SetLineup(p1, NewLineup()));
    }

    [Fact]
    public void SetLineup_PlayerTwoLineupAlreadySet_ThrowsDomainException()
    {
        var (game, _, p2) = NewGame();
        game.SetLineup(p2, NewLineup());
        Assert.Throws<DomainException>(() => game.SetLineup(p2, NewLineup()));
    }

    [Fact]
    public void SetLineup_WithNullLineup_ThrowsDomainException()
    {
        var (game, p1, _) = NewGame();
        Assert.Throws<DomainException>(() => game.SetLineup(p1, null!));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 3. Start()
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Start_SetsStatusToInProgress()
    {
        var (game, p1, p2) = NewGame();
        game.SetLineup(p1, NewLineup());
        game.SetLineup(p2, NewLineup());
        game.Start();
        Assert.Equal(GameStatus.InProgress, game.Status);
    }

    [Fact]
    public void Start_SetsTurnNumberToOne()
    {
        var (game, p1, p2) = NewGame();
        game.SetLineup(p1, NewLineup());
        game.SetLineup(p2, NewLineup());
        game.Start();
        Assert.Equal(1, game.TurnNumber);
    }

    [Fact]
    public void Start_RaisesGameStartedEvent()
    {
        var (game, p1, p2) = NewGame();
        game.SetLineup(p1, NewLineup());
        game.SetLineup(p2, NewLineup());
        game.Start();
        Assert.Single(game.DomainEvents.OfType<GameStarted>());
    }

    [Fact]
    public void Start_GameStartedEvent_ContainsCorrectGameId()
    {
        var gameId = Guid.NewGuid();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = new Game(gameId, p1, p2, NewBoard());
        game.SetLineup(p1, NewLineup());
        game.SetLineup(p2, NewLineup());
        game.Start();

        var evt = game.DomainEvents.OfType<GameStarted>().Single();
        Assert.Equal(gameId, evt.GameId);
    }

    [Fact]
    public void Start_GameStartedEvent_ContainsCorrectPlayerOne()
    {
        var (game, p1, p2) = NewGame();
        game.SetLineup(p1, NewLineup());
        game.SetLineup(p2, NewLineup());
        game.Start();

        var evt = game.DomainEvents.OfType<GameStarted>().Single();
        Assert.Equal(p1, evt.PlayerOne);
    }

    [Fact]
    public void Start_GameStartedEvent_ContainsCorrectPlayerTwo()
    {
        var (game, p1, p2) = NewGame();
        game.SetLineup(p1, NewLineup());
        game.SetLineup(p2, NewLineup());
        game.Start();

        var evt = game.DomainEvents.OfType<GameStarted>().Single();
        Assert.Equal(p2, evt.PlayerTwo);
    }

    [Fact]
    public void Start_WhenAlreadyInProgress_ThrowsDomainException()
    {
        var (game, p1, p2) = StartedGame();
        Assert.Throws<DomainException>(() => game.Start());
    }

    [Fact]
    public void Start_WhenFinished_ThrowsDomainException()
    {
        var (game, p1, p2) = StartedGame();
        game.End();
        Assert.Throws<DomainException>(() => game.Start());
    }

    [Fact]
    public void Start_WithoutPlayerOneLineup_ThrowsDomainException()
    {
        var (game, _, p2) = NewGame();
        game.SetLineup(p2, NewLineup());
        Assert.Throws<DomainException>(() => game.Start());
    }

    [Fact]
    public void Start_WithoutPlayerTwoLineup_ThrowsDomainException()
    {
        var (game, p1, _) = NewGame();
        game.SetLineup(p1, NewLineup());
        Assert.Throws<DomainException>(() => game.Start());
    }

    [Fact]
    public void Start_WithoutEitherLineup_ThrowsDomainException()
    {
        var (game, _, _) = NewGame();
        Assert.Throws<DomainException>(() => game.Start());
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 4. End()
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void End_SetsStatusToFinished()
    {
        var (game, _, _) = StartedGame();
        game.End();
        Assert.Equal(GameStatus.Finished, game.Status);
    }

    [Fact]
    public void End_WhenPlayerOneHasHigherScore_WinnerIsPlayerOne()
    {
        var (game, p1, _) = StartedGame();
        game.AddScore(p1, 10);
        game.End();

        var evt = game.DomainEvents.OfType<GameEnded>().Single();
        Assert.Equal(p1, evt.WinnerId);
    }

    [Fact]
    public void End_WhenPlayerTwoHasHigherScore_WinnerIsPlayerTwo()
    {
        var (game, _, p2) = StartedGame();
        game.AddScore(p2, 10);
        game.End();

        var evt = game.DomainEvents.OfType<GameEnded>().Single();
        Assert.Equal(p2, evt.WinnerId);
    }

    [Fact]
    public void End_WhenScoresAreEqual_IsDraw()
    {
        var (game, _, _) = StartedGame();
        game.End();

        var evt = game.DomainEvents.OfType<GameEnded>().Single();
        Assert.True(evt.IsDraw);
    }

    [Fact]
    public void End_WhenDraw_WinnerIdIsNull()
    {
        var (game, _, _) = StartedGame();
        game.End();

        var evt = game.DomainEvents.OfType<GameEnded>().Single();
        Assert.Null(evt.WinnerId);
    }

    [Fact]
    public void End_WhenNotDraw_IsDraw_IsFalse()
    {
        var (game, p1, _) = StartedGame();
        game.AddScore(p1, 5);
        game.End();

        var evt = game.DomainEvents.OfType<GameEnded>().Single();
        Assert.False(evt.IsDraw);
    }

    [Fact]
    public void End_RaisesGameEndedEvent()
    {
        var (game, _, _) = StartedGame();
        game.End();
        Assert.Single(game.DomainEvents.OfType<GameEnded>());
    }

    [Fact]
    public void End_GameEndedEvent_ContainsCorrectGameId()
    {
        var gameId = Guid.NewGuid();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = new Game(gameId, p1, p2, NewBoard());
        game.SetLineup(p1, NewLineup());
        game.SetLineup(p2, NewLineup());
        game.Start();
        game.End();

        var evt = game.DomainEvents.OfType<GameEnded>().Single();
        Assert.Equal(gameId, evt.GameId);
    }

    [Fact]
    public void End_GameEndedEvent_PlayerOneScore_IsCorrect()
    {
        var (game, p1, _) = StartedGame();
        game.AddScore(p1, 7);
        game.End();

        var evt = game.DomainEvents.OfType<GameEnded>().Single();
        Assert.Equal(7, evt.PlayerOneScore);
    }

    [Fact]
    public void End_GameEndedEvent_PlayerTwoScore_IsCorrect()
    {
        var (game, _, p2) = StartedGame();
        game.AddScore(p2, 3);
        game.End();

        var evt = game.DomainEvents.OfType<GameEnded>().Single();
        Assert.Equal(3, evt.PlayerTwoScore);
    }

    [Fact]
    public void End_WhenNotInProgress_ThrowsDomainException()
    {
        var (game, _, _) = NewGame();
        Assert.Throws<DomainException>(() => game.End());
    }

    [Fact]
    public void End_WhenAlreadyFinished_ThrowsDomainException()
    {
        var (game, _, _) = StartedGame();
        game.End();
        Assert.Throws<DomainException>(() => game.End());
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 5. AddScore()
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AddScore_ForPlayerOne_IncrementsPlayerOneScore()
    {
        var (game, p1, _) = StartedGame();
        game.AddScore(p1, 5);
        Assert.Equal(5, game.Scores[p1]);
    }

    [Fact]
    public void AddScore_ForPlayerTwo_IncrementsPlayerTwoScore()
    {
        var (game, _, p2) = StartedGame();
        game.AddScore(p2, 3);
        Assert.Equal(3, game.Scores[p2]);
    }

    [Fact]
    public void AddScore_MultipleCallsForSamePlayer_AccumulatesCorrectly()
    {
        var (game, p1, _) = StartedGame();
        game.AddScore(p1, 2);
        game.AddScore(p1, 3);
        game.AddScore(p1, 5);
        Assert.Equal(10, game.Scores[p1]);
    }

    [Fact]
    public void AddScore_ForNonParticipant_ThrowsDomainException()
    {
        var (game, _, _) = StartedGame();
        var stranger = Guid.NewGuid();
        Assert.Throws<DomainException>(() => game.AddScore(stranger, 5));
    }

    [Fact]
    public void AddScore_WithNegativePoints_ThrowsDomainException()
    {
        var (game, p1, _) = StartedGame();
        Assert.Throws<DomainException>(() => game.AddScore(p1, -1));
    }

    [Fact]
    public void AddScore_WithZeroPoints_DoesNotThrow()
    {
        var (game, p1, _) = StartedGame();
        var exception = Record.Exception(() => game.AddScore(p1, 0));
        Assert.Null(exception);
    }

    [Fact]
    public void AddScore_WhenNotInProgress_ThrowsDomainException()
    {
        var (game, p1, _) = NewGame();
        Assert.Throws<DomainException>(() => game.AddScore(p1, 5));
    }

    [Fact]
    public void AddScore_WhenFinished_ThrowsDomainException()
    {
        var (game, p1, _) = StartedGame();
        game.End();
        Assert.Throws<DomainException>(() => game.AddScore(p1, 5));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 6. AddPieceToBoard() / RemovePieceFromBoard()
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AddPieceToBoard_CanAddUpToMaxPiecesPerPlayer()
    {
        var (game, p1, _) = StartedGame();
        for (var i = 0; i < Game.MaxPiecesOnBoard; i++)
            game.AddPieceToBoard(p1);
        Assert.Equal(Game.MaxPiecesOnBoard, game.PiecesOnBoard[p1]);
    }

    [Fact]
    public void AddPieceToBoard_FourthPiece_ThrowsDomainException()
    {
        var (game, p1, _) = StartedGame();
        for (var i = 0; i < Game.MaxPiecesOnBoard; i++)
            game.AddPieceToBoard(p1);
        Assert.Throws<DomainException>(() => game.AddPieceToBoard(p1));
    }

    [Fact]
    public void AddPieceToBoard_PlayerOneLimitDoesNotAffectPlayerTwo()
    {
        var (game, p1, p2) = StartedGame();
        for (var i = 0; i < Game.MaxPiecesOnBoard; i++)
            game.AddPieceToBoard(p1);
        var exception = Record.Exception(() => game.AddPieceToBoard(p2));
        Assert.Null(exception);
    }

    [Fact]
    public void AddPieceToBoard_ForNonParticipant_ThrowsDomainException()
    {
        var (game, _, _) = StartedGame();
        var stranger = Guid.NewGuid();
        Assert.Throws<DomainException>(() => game.AddPieceToBoard(stranger));
    }

    [Fact]
    public void AddPieceToBoard_WhenWaitingForBots_ThrowsDomainException()
    {
        var (game, p1, _) = NewGame();
        Assert.Throws<DomainException>(() => game.AddPieceToBoard(p1));
    }

    [Fact]
    public void AddPieceToBoard_WhenFinished_ThrowsDomainException()
    {
        var (game, p1, _) = StartedGame();
        game.End();
        Assert.Throws<DomainException>(() => game.AddPieceToBoard(p1));
    }

    [Fact]
    public void RemovePieceFromBoard_AfterAdding_DecrementsCount()
    {
        var (game, p1, _) = StartedGame();
        game.AddPieceToBoard(p1);
        game.RemovePieceFromBoard(p1);
        Assert.Equal(0, game.PiecesOnBoard[p1]);
    }

    [Fact]
    public void RemovePieceFromBoard_WhenNoPiecesOnBoard_ThrowsDomainException()
    {
        var (game, p1, _) = StartedGame();
        Assert.Throws<DomainException>(() => game.RemovePieceFromBoard(p1));
    }

    [Fact]
    public void RemovePieceFromBoard_ForNonParticipant_ThrowsDomainException()
    {
        var (game, _, _) = StartedGame();
        var stranger = Guid.NewGuid();
        Assert.Throws<DomainException>(() => game.RemovePieceFromBoard(stranger));
    }

    [Fact]
    public void RemovePieceFromBoard_WhenWaitingForBots_ThrowsDomainException()
    {
        var (game, p1, _) = NewGame();
        Assert.Throws<DomainException>(() => game.RemovePieceFromBoard(p1));
    }

    [Fact]
    public void RemovePieceFromBoard_WhenFinished_ThrowsDomainException()
    {
        var (game, p1, _) = StartedGame();
        game.End();
        Assert.Throws<DomainException>(() => game.RemovePieceFromBoard(p1));
    }

    [Fact]
    public void AddThenRemovePiece_AllowsAddingAgain()
    {
        var (game, p1, _) = StartedGame();
        for (var i = 0; i < Game.MaxPiecesOnBoard; i++)
            game.AddPieceToBoard(p1);
        game.RemovePieceFromBoard(p1);
        var exception = Record.Exception(() => game.AddPieceToBoard(p1));
        Assert.Null(exception);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 7. AdvanceTurn()
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AdvanceTurn_FromTurnOne_SetsTurnNumberToTwo()
    {
        var (game, _, _) = StartedGame();
        AdvanceToMovePhase(game);
        game.AdvanceTurn();
        Assert.Equal(2, game.TurnNumber);
    }

    [Fact]
    public void AdvanceTurn_FromTurnTwo_SetsTurnNumberToThree()
    {
        var (game, _, _) = StartedGame();
        AdvanceToMovePhase(game);
        game.AdvanceTurn();
        AdvanceToMovePhase(game);
        game.AdvanceTurn();
        Assert.Equal(3, game.TurnNumber);
    }

    [Fact]
    public void AdvanceTurn_ThroughAllFiveTurns_EndsGame()
    {
        var (game, _, _) = StartedGame();
        for (var i = 0; i < Game.TotalTurns; i++)
        {
            AdvanceToMovePhase(game);
            game.AdvanceTurn();
        }
        Assert.Equal(GameStatus.Finished, game.Status);
    }

    [Fact]
    public void AdvanceTurn_FromTurnFive_CallsEnd_SetsStatusFinished()
    {
        var (game, _, _) = StartedGame();
        // Advance to turn 5
        for (var i = 0; i < Game.TotalTurns - 1; i++)
        {
            AdvanceToMovePhase(game);
            game.AdvanceTurn();
        }
        Assert.Equal(Game.TotalTurns, game.TurnNumber);
        // Advance from turn 5 → auto-End
        AdvanceToMovePhase(game);
        game.AdvanceTurn();
        Assert.Equal(GameStatus.Finished, game.Status);
    }

    [Fact]
    public void AdvanceTurn_FromTurnFive_RaisesGameEndedEvent()
    {
        var (game, _, _) = StartedGame();
        for (var i = 0; i < Game.TotalTurns - 1; i++)
        {
            AdvanceToMovePhase(game);
            game.AdvanceTurn();
        }
        game.ClearDomainEvents();
        AdvanceToMovePhase(game);
        game.AdvanceTurn();
        Assert.Single(game.DomainEvents.OfType<GameEnded>());
    }

    [Fact]
    public void AdvanceTurn_WhenNotInProgress_ThrowsDomainException()
    {
        var (game, _, _) = NewGame();
        Assert.Throws<DomainException>(() => game.AdvanceTurn());
    }

    [Fact]
    public void AdvanceTurn_WhenFinished_ThrowsDomainException()
    {
        var (game, _, _) = StartedGame();
        game.End();
        Assert.Throws<DomainException>(() => game.AdvanceTurn());
    }

    [Fact]
    public void AdvanceTurn_DoesNotExceedTotalTurns_BeforeEnding()
    {
        var (game, _, _) = StartedGame();
        for (var i = 0; i < Game.TotalTurns - 1; i++)
        {
            AdvanceToMovePhase(game);
            game.AdvanceTurn();
        }
        Assert.Equal(Game.TotalTurns, game.TurnNumber);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 8. DomainEvents
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DomainEvents_OnConstruction_IsEmpty()
    {
        var (game, _, _) = NewGame();
        Assert.Empty(game.DomainEvents);
    }

    [Fact]
    public void ClearDomainEvents_AfterStart_RemovesAllEvents()
    {
        var (game, p1, p2) = NewGame();
        game.SetLineup(p1, NewLineup());
        game.SetLineup(p2, NewLineup());
        game.Start();
        game.ClearDomainEvents();
        Assert.Empty(game.DomainEvents);
    }

    [Fact]
    public void ClearDomainEvents_AfterEnd_RemovesAllEvents()
    {
        var (game, _, _) = StartedGame();
        game.End();
        game.ClearDomainEvents();
        Assert.Empty(game.DomainEvents);
    }

    [Fact]
    public void ClearDomainEvents_CalledOnEmptyList_DoesNotThrow()
    {
        var (game, _, _) = NewGame();
        var exception = Record.Exception(() => game.ClearDomainEvents());
        Assert.Null(exception);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 9. GetScore / GetPiecesOnBoardCount helpers
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetScore_ForParticipant_ReturnsCurrentScore()
    {
        var (game, p1, _) = StartedGame();
        game.AddScore(p1, 7);
        Assert.Equal(7, game.GetScore(p1));
    }

    [Fact]
    public void GetScore_ForNonParticipant_ThrowsDomainException()
    {
        var (game, _, _) = StartedGame();
        Assert.Throws<DomainException>(() => game.GetScore(Guid.NewGuid()));
    }

    [Fact]
    public void GetPiecesOnBoardCount_AfterAddingPiece_ReturnsOne()
    {
        var (game, p1, _) = StartedGame();
        game.AddPieceToBoard(p1);
        Assert.Equal(1, game.GetPiecesOnBoardCount(p1));
    }

    [Fact]
    public void GetPiecesOnBoardCount_ForNonParticipant_ThrowsDomainException()
    {
        var (game, _, _) = StartedGame();
        Assert.Throws<DomainException>(() => game.GetPiecesOnBoardCount(Guid.NewGuid()));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 10. AdvancePhase() and phase guards
    // ══════════════════════════════════════════════════════════════════════════

    // ── AdvancePhase() — individual transitions ───────────────────────────────

    [Fact]
    public void AdvancePhase_FromCoinSpawn_SetsCurrentPhaseToPlacePhase()
    {
        var (game, _, _) = StartedGame();
        // freshly started game is in CoinSpawn
        game.AdvancePhase();
        Assert.Equal(TurnPhase.PlacePhase, game.CurrentPhase);
    }

    [Fact]
    public void AdvancePhase_FromPlacePhase_SetsCurrentPhaseToMovePhase()
    {
        var (game, _, _) = StartedGame();
        game.AdvancePhase(); // CoinSpawn → PlacePhase
        game.AdvancePhase(); // PlacePhase → MovePhase
        Assert.Equal(TurnPhase.MovePhase, game.CurrentPhase);
    }

    [Fact]
    public void AdvancePhase_FromMovePhase_OnNonFinalTurn_IncrementsTurnNumber()
    {
        var (game, _, _) = StartedGame();
        AdvanceToMovePhase(game);
        var turnBefore = game.TurnNumber;
        game.AdvancePhase(); // MovePhase → CoinSpawn, turn++
        Assert.Equal(turnBefore + 1, game.TurnNumber);
    }

    [Fact]
    public void AdvancePhase_FromMovePhase_OnNonFinalTurn_ResetsCurrentPhaseToCoinSpawn()
    {
        var (game, _, _) = StartedGame();
        AdvanceToMovePhase(game);
        game.AdvancePhase();
        Assert.Equal(TurnPhase.CoinSpawn, game.CurrentPhase);
    }

    [Fact]
    public void AdvancePhase_FromMovePhase_OnTurnFive_SetsStatusToFinished()
    {
        var (game, _, _) = StartedGame();
        // advance through turns 1-4
        for (var i = 0; i < Game.TotalTurns - 1; i++)
        {
            AdvanceToMovePhase(game);
            game.AdvancePhase(); // MovePhase → CoinSpawn, turn++
        }
        // now on turn 5 — advance to MovePhase then trigger final AdvancePhase
        AdvanceToMovePhase(game);
        game.AdvancePhase(); // MovePhase on turn 5 → End()
        Assert.Equal(GameStatus.Finished, game.Status);
    }

    [Fact]
    public void AdvancePhase_WhenNotInProgress_ThrowsDomainException()
    {
        var (game, _, _) = NewGame();
        Assert.Throws<DomainException>(() => game.AdvancePhase());
    }

    [Fact]
    public void AdvancePhase_WhenFinished_ThrowsDomainException()
    {
        var (game, _, _) = StartedGame();
        game.End();
        Assert.Throws<DomainException>(() => game.AdvancePhase());
    }

    [Fact]
    public void AdvancePhase_FullFiveTurnCycle_AllFifteenTransitions_EndsGameAsFinished()
    {
        var (game, _, _) = StartedGame();
        // 5 turns × 3 phases each = 15 calls to AdvancePhase
        for (var turn = 0; turn < Game.TotalTurns; turn++)
        {
            game.AdvancePhase(); // CoinSpawn → PlacePhase
            game.AdvancePhase(); // PlacePhase → MovePhase
            game.AdvancePhase(); // MovePhase → CoinSpawn (or End on turn 5)
        }
        Assert.Equal(GameStatus.Finished, game.Status);
    }

    // ── EnsureInCoinSpawnPhase() ──────────────────────────────────────────────

    [Fact]
    public void EnsureInCoinSpawnPhase_WhenInCoinSpawn_DoesNotThrow()
    {
        var (game, _, _) = StartedGame();
        // freshly started game is in CoinSpawn
        var exception = Record.Exception(() => game.EnsureInCoinSpawnPhase());
        Assert.Null(exception);
    }

    [Fact]
    public void EnsureInCoinSpawnPhase_WhenInPlacePhase_ThrowsDomainException()
    {
        var (game, _, _) = StartedGame();
        game.AdvancePhase(); // CoinSpawn → PlacePhase
        Assert.Throws<DomainException>(() => game.EnsureInCoinSpawnPhase());
    }

    [Fact]
    public void EnsureInCoinSpawnPhase_WhenInMovePhase_ThrowsDomainException()
    {
        var (game, _, _) = StartedGame();
        AdvanceToMovePhase(game);
        Assert.Throws<DomainException>(() => game.EnsureInCoinSpawnPhase());
    }

    [Fact]
    public void EnsureInCoinSpawnPhase_WhenCurrentPhaseIsNull_ThrowsDomainException()
    {
        var (game, _, _) = StartedGame();
        game.End(); // sets CurrentPhase = null
        Assert.Throws<DomainException>(() => game.EnsureInCoinSpawnPhase());
    }

    // ── EnsureInPlacePhase() ──────────────────────────────────────────────────

    [Fact]
    public void EnsureInPlacePhase_WhenInPlacePhase_DoesNotThrow()
    {
        var (game, _, _) = StartedGame();
        game.AdvancePhase(); // CoinSpawn → PlacePhase
        var exception = Record.Exception(() => game.EnsureInPlacePhase());
        Assert.Null(exception);
    }

    [Fact]
    public void EnsureInPlacePhase_WhenInCoinSpawnPhase_ThrowsDomainException()
    {
        var (game, _, _) = StartedGame();
        // freshly started game is in CoinSpawn
        Assert.Throws<DomainException>(() => game.EnsureInPlacePhase());
    }

    [Fact]
    public void EnsureInPlacePhase_WhenInMovePhase_ThrowsDomainException()
    {
        var (game, _, _) = StartedGame();
        AdvanceToMovePhase(game);
        Assert.Throws<DomainException>(() => game.EnsureInPlacePhase());
    }

    [Fact]
    public void EnsureInPlacePhase_WhenCurrentPhaseIsNull_ThrowsDomainException()
    {
        var (game, _, _) = StartedGame();
        game.End(); // sets CurrentPhase = null
        Assert.Throws<DomainException>(() => game.EnsureInPlacePhase());
    }

    // ── EnsureInMovePhase() ───────────────────────────────────────────────────

    [Fact]
    public void EnsureInMovePhase_WhenInMovePhase_DoesNotThrow()
    {
        var (game, _, _) = StartedGame();
        AdvanceToMovePhase(game);
        var exception = Record.Exception(() => game.EnsureInMovePhase());
        Assert.Null(exception);
    }

    [Fact]
    public void EnsureInMovePhase_WhenInCoinSpawnPhase_ThrowsDomainException()
    {
        var (game, _, _) = StartedGame();
        // freshly started game is in CoinSpawn
        Assert.Throws<DomainException>(() => game.EnsureInMovePhase());
    }

    [Fact]
    public void EnsureInMovePhase_WhenInPlacePhase_ThrowsDomainException()
    {
        var (game, _, _) = StartedGame();
        game.AdvancePhase(); // CoinSpawn → PlacePhase
        Assert.Throws<DomainException>(() => game.EnsureInMovePhase());
    }

    [Fact]
    public void EnsureInMovePhase_WhenCurrentPhaseIsNull_ThrowsDomainException()
    {
        var (game, _, _) = StartedGame();
        game.End(); // sets CurrentPhase = null
        Assert.Throws<DomainException>(() => game.EnsureInMovePhase());
    }

    // ── TurnPhaseAdvanced domain events ───────────────────────────────────────

    [Fact]
    public void AdvancePhase_CoinSpawnToPlacePhase_RaisesTurnPhaseAdvancedEvent()
    {
        var (game, _, _) = StartedGame();
        game.ClearDomainEvents();
        game.AdvancePhase(); // CoinSpawn → PlacePhase
        Assert.Single(game.DomainEvents.OfType<TurnPhaseAdvanced>());
    }

    [Fact]
    public void AdvancePhase_CoinSpawnToPlacePhase_Event_HasCorrectTurnNumber()
    {
        var (game, _, _) = StartedGame();
        game.ClearDomainEvents();
        game.AdvancePhase();
        var evt = game.DomainEvents.OfType<TurnPhaseAdvanced>().Single();
        Assert.Equal(1, evt.TurnNumber);
    }

    [Fact]
    public void AdvancePhase_CoinSpawnToPlacePhase_Event_HasCorrectPreviousPhase()
    {
        var (game, _, _) = StartedGame();
        game.ClearDomainEvents();
        game.AdvancePhase();
        var evt = game.DomainEvents.OfType<TurnPhaseAdvanced>().Single();
        Assert.Equal(TurnPhase.CoinSpawn, evt.PreviousPhase);
    }

    [Fact]
    public void AdvancePhase_CoinSpawnToPlacePhase_Event_HasCorrectNewPhase()
    {
        var (game, _, _) = StartedGame();
        game.ClearDomainEvents();
        game.AdvancePhase();
        var evt = game.DomainEvents.OfType<TurnPhaseAdvanced>().Single();
        Assert.Equal(TurnPhase.PlacePhase, evt.NewPhase);
    }

    [Fact]
    public void AdvancePhase_PlacePhaseToMovePhase_RaisesTurnPhaseAdvancedEvent()
    {
        var (game, _, _) = StartedGame();
        game.AdvancePhase(); // CoinSpawn → PlacePhase
        game.ClearDomainEvents();
        game.AdvancePhase(); // PlacePhase → MovePhase
        Assert.Single(game.DomainEvents.OfType<TurnPhaseAdvanced>());
    }

    [Fact]
    public void AdvancePhase_PlacePhaseToMovePhase_Event_HasCorrectPreviousPhase()
    {
        var (game, _, _) = StartedGame();
        game.AdvancePhase();
        game.ClearDomainEvents();
        game.AdvancePhase();
        var evt = game.DomainEvents.OfType<TurnPhaseAdvanced>().Single();
        Assert.Equal(TurnPhase.PlacePhase, evt.PreviousPhase);
    }

    [Fact]
    public void AdvancePhase_PlacePhaseToMovePhase_Event_HasCorrectNewPhase()
    {
        var (game, _, _) = StartedGame();
        game.AdvancePhase();
        game.ClearDomainEvents();
        game.AdvancePhase();
        var evt = game.DomainEvents.OfType<TurnPhaseAdvanced>().Single();
        Assert.Equal(TurnPhase.MovePhase, evt.NewPhase);
    }

    [Fact]
    public void AdvancePhase_MovePhaseToCoinSpawn_OnNonFinalTurn_Event_HasOldTurnNumber()
    {
        var (game, _, _) = StartedGame();
        AdvanceToMovePhase(game);
        var oldTurn = game.TurnNumber; // 1
        game.ClearDomainEvents();
        game.AdvancePhase(); // MovePhase → CoinSpawn, turn++
        var evt = game.DomainEvents.OfType<TurnPhaseAdvanced>().Single();
        Assert.Equal(oldTurn, evt.TurnNumber);
    }

    [Fact]
    public void AdvancePhase_MovePhaseToCoinSpawn_OnNonFinalTurn_Event_HasCorrectNewPhase()
    {
        var (game, _, _) = StartedGame();
        AdvanceToMovePhase(game);
        game.ClearDomainEvents();
        game.AdvancePhase();
        var evt = game.DomainEvents.OfType<TurnPhaseAdvanced>().Single();
        Assert.Equal(TurnPhase.CoinSpawn, evt.NewPhase);
    }

    [Fact]
    public void AdvancePhase_FinalMovePhase_Event_HasNullNewPhase()
    {
        var (game, _, _) = StartedGame();
        // advance to turn 5 MovePhase
        for (var i = 0; i < Game.TotalTurns - 1; i++)
        {
            AdvanceToMovePhase(game);
            game.AdvancePhase(); // complete non-final turns
        }
        AdvanceToMovePhase(game);
        game.ClearDomainEvents();
        game.AdvancePhase(); // final MovePhase → End
        var evt = game.DomainEvents.OfType<TurnPhaseAdvanced>().Single();
        Assert.Null(evt.NewPhase);
    }

    [Fact]
    public void AdvancePhase_FinalMovePhase_Event_HasCorrectPreviousPhase()
    {
        var (game, _, _) = StartedGame();
        for (var i = 0; i < Game.TotalTurns - 1; i++)
        {
            AdvanceToMovePhase(game);
            game.AdvancePhase();
        }
        AdvanceToMovePhase(game);
        game.ClearDomainEvents();
        game.AdvancePhase();
        var evt = game.DomainEvents.OfType<TurnPhaseAdvanced>().Single();
        Assert.Equal(TurnPhase.MovePhase, evt.PreviousPhase);
    }

    [Fact]
    public void AdvanceTurn_RaisesTurnPhaseAdvancedEvent()
    {
        var (game, _, _) = StartedGame();
        AdvanceToMovePhase(game);
        game.ClearDomainEvents();
        game.AdvanceTurn(); // delegates to AdvancePhase()
        Assert.Single(game.DomainEvents.OfType<TurnPhaseAdvanced>());
    }

    // ── AdvanceTurn() guard ───────────────────────────────────────────────────

    [Fact]
    public void AdvanceTurn_WhenInCoinSpawnPhase_ThrowsDomainException()
    {
        var (game, _, _) = StartedGame();
        // freshly started game is in CoinSpawn — AdvanceTurn requires MovePhase
        Assert.Throws<DomainException>(() => game.AdvanceTurn());
    }

    [Fact]
    public void AdvanceTurn_WhenInPlacePhase_ThrowsDomainException()
    {
        var (game, _, _) = StartedGame();
        game.AdvancePhase(); // CoinSpawn → PlacePhase
        Assert.Throws<DomainException>(() => game.AdvanceTurn());
    }
}
