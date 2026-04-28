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
}
