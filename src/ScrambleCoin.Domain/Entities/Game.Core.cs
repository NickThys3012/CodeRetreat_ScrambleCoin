using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Events;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Entities;

/// <summary>
/// Core game state: initialization, properties, and basic state transitions.
/// Handles constructor, identity, board, lineups, scores, turn/phase tracking, and domain events.
/// </summary>
public partial class Game
{
    /// <summary>The total number of turns in a game.</summary>
    public const int TotalTurns = 5;

    /// <summary>Maximum number of pieces a player may have on the board at any time.</summary>
    public const int MaxPiecesOnBoard = 3;

    // ── Domain events ─────────────────────────────────────────────────────────

    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Domain events raised by this aggregate since it was last loaded.</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Clears all collected domain events (call after dispatching).</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    // ── Identity & participants ───────────────────────────────────────────────

    /// <summary>Unique identifier for this game.</summary>
    public Guid Id { get; }

    /// <summary>Identifier of player one.</summary>
    public Guid PlayerOne { get; }

    /// <summary>Identifier of player two.</summary>
    public Guid PlayerTwo { get; }

    // ── Board ─────────────────────────────────────────────────────────────────

    /// <summary>The game board.</summary>
    public Board Board { get; }

    // ── Lineups ───────────────────────────────────────────────────────────────

    /// <summary>Player one's selected lineup; <c>null</c> until <see cref="SetLineup"/> is called.</summary>
    public Lineup? LineupPlayerOne { get; private set; }

    /// <summary>Player two's selected lineup; <c>null</c> until <see cref="SetLineup"/> is called.</summary>
    public Lineup? LineupPlayerTwo { get; private set; }

    // ── Scores ────────────────────────────────────────────────────────────────

    private readonly Dictionary<Guid, int> _scores;

    /// <summary>Per-player scores, keyed by player identifier.</summary>
    public IReadOnlyDictionary<Guid, int> Scores => _scores;

    // ── Turn & status ─────────────────────────────────────────────────────────

    /// <summary>Current turn number (1–5); 0 before the game starts.</summary>
    public int TurnNumber { get; private set; }

    /// <summary>
    /// Current turn number (1–5); 0 before the game starts.
    /// Alias for <see cref="TurnNumber"/> that matches the phase-based naming convention.
    /// </summary>
    public int CurrentTurnNumber => TurnNumber;

    /// <summary>Current lifecycle status of the game.</summary>
    public GameStatus Status { get; private set; }

    /// <summary>
    /// The active phase within the current turn (<see cref="TurnPhase.CoinSpawn"/>,
    /// <see cref="TurnPhase.PlacePhase"/>, or <see cref="TurnPhase.MovePhase"/>).
    /// <c>null</c> when the game has not yet started or has already finished.
    /// </summary>
    public TurnPhase? CurrentPhase { get; private set; }

    // ── Place-phase tracking ──────────────────────────────────────────────────

    private readonly HashSet<Guid> _placePhaseDone = [];

    // ── Move-phase tracking ───────────────────────────────────────────────────

    private readonly HashSet<Guid> _movedPieceIds = [];

    /// <summary>
    /// The player whose turn it currently is to submit a piece moves during MovePhase.
    /// PlayerOne always moves first; switches to PlayerTwo once all of PlayerOne's
    /// on-board pieces have moved. <c>null</c> outside MovePhase.
    /// </summary>
    public Guid? MovePhaseActivePlayer { get; private set; }

    // ── Pieces-on-board counters ──────────────────────────────────────────────

    private readonly Dictionary<Guid, int> _piecesOnBoard;

    /// <summary>
    /// Number of pieces each player currently has on the board, keyed by player identifier.
    /// </summary>
    public IReadOnlyDictionary<Guid, int> PiecesOnBoard => _piecesOnBoard;

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new <see cref="Game"/> in the <see cref="GameStatus.WaitingForBots"/> state.
    /// </summary>
    /// <param name="id">Unique game identifier.</param>
    /// <param name="playerOne">Identifier of player one.</param>
    /// <param name="playerTwo">Identifier of player two.</param>
    /// <param name="board">The pre-constructed game board.</param>
    /// <exception cref="DomainException">
    /// Thrown when <paramref name="playerOne"/> equals <paramref name="playerTwo"/>,
    /// or when <paramref name="board"/> is null.
    /// </exception>
    public Game(Guid id, Guid playerOne, Guid playerTwo, Board board)
    {
        if (playerOne == playerTwo)
            throw new DomainException("PlayerOne and PlayerTwo must be different players.");

        Id = id;
        PlayerOne = playerOne;
        PlayerTwo = playerTwo;
        Board = board ?? throw new DomainException("Board must not be null.");
        Status = GameStatus.WaitingForBots;
        TurnNumber = 0;
        CurrentPhase = null;

        _scores = new Dictionary<Guid, int>
        {
            [playerOne] = 0,
            [playerTwo] = 0
        };

        _piecesOnBoard = new Dictionary<Guid, int>
        {
            [playerOne] = 0,
            [playerTwo] = 0
        };
    }

    /// <summary>
    /// Convenience constructor that generates a new <see cref="Id"/>.
    /// </summary>
    public Game(Guid playerOne, Guid playerTwo, Board board)
        : this(Guid.NewGuid(), playerOne, playerTwo, board)
    {
    }

    /// <summary>
    /// Creates a new game shell in <see cref="GameStatus.WaitingForBots"/> state,
    /// with two randomly generated player slot identifiers.
    /// Bots joining via <c>POST /api/games/{gameId}/join</c> will receive one of these
    /// slot IDs as their <c>playerId</c> and use it for all later game actions.
    /// </summary>
    /// <param name="board">The pre-constructed game board.</param>
    /// <returns>A new <see cref="Game"/> with empty lineups awaiting bot registration.</returns>
    public static Game CreateShell(Board board) =>
        new(Guid.NewGuid(), Guid.NewGuid(), board);

    // ── Lineup management ─────────────────────────────────────────────────────

    /// <summary>
    /// Stores the lineup for <paramref name="playerId"/>.
    /// </summary>
    /// <remarks>Only allowed while the game is in <see cref="GameStatus.WaitingForBots"/>.</remarks>
    /// <exception cref="DomainException">
    /// Thrown when the game is not in <see cref="GameStatus.WaitingForBots"/> state,
    /// when <paramref name="playerId"/> is not a participant of this game,
    /// or when <paramref name="lineup"/> is null.
    /// </exception>
    public void SetLineup(Guid playerId, Lineup lineup)
    {
        if (Status != GameStatus.WaitingForBots)
            throw new DomainException(
                $"Lineups can only be set while the game is in {GameStatus.WaitingForBots} state. Current status: {Status}.");

        if (lineup is null)
            throw new DomainException("Lineup must not be null.");

        if (playerId == PlayerOne)
        {
            if (LineupPlayerOne is not null)
                throw new DomainException("PlayerOne's lineup has already been set.");
            LineupPlayerOne = lineup;
        }
        else if (playerId == PlayerTwo)
        {
            if (LineupPlayerTwo is not null)
                throw new DomainException("PlayerTwo's lineup has already been set.");
            LineupPlayerTwo = lineup;
        }
        else
            throw new DomainException($"Player {playerId} is not a participant of game {Id}.");
    }

    // ── State transitions ─────────────────────────────────────────────────────

    /// <summary>
    /// Starts the game: transitions <see cref="GameStatus.WaitingForBots"/> → <see cref="GameStatus.InProgress"/>,
    /// sets <see cref="TurnNumber"/> to 1, and raises <see cref="GameStarted"/>.
    /// </summary>
    /// <exception cref="DomainException">
    /// Thrown when the game is not in <see cref="GameStatus.WaitingForBots"/> state,
    /// or when one or both lineups have not been set.
    /// </exception>
    public void Start()
    {
        if (Status != GameStatus.WaitingForBots)
            throw new DomainException(
                $"Game can only be started from {GameStatus.WaitingForBots} state. Current status: {Status}.");

        if (LineupPlayerOne is null)
            throw new DomainException("PlayerOne's lineup has not been set.");

        if (LineupPlayerTwo is null)
            throw new DomainException("PlayerTwo's lineup has not been set.");

        Status = GameStatus.InProgress;
        TurnNumber = 1;
        CurrentPhase = TurnPhase.CoinSpawn;

        _domainEvents.Add(new GameStarted(Id, PlayerOne, PlayerTwo, DateTimeOffset.UtcNow));

        // Apply turn-start abilities for turn 1 (Issue #50)
        OnTurnStart();
    }

    /// <summary>
    /// Ends the game: transitions <see cref="GameStatus.InProgress"/> → <see cref="GameStatus.Finished"/>,
    /// determines the winner (or draw) and raises <see cref="GameEnded"/>.
    /// </summary>
    /// <exception cref="DomainException">
    /// Thrown when the game is not in <see cref="GameStatus.InProgress"/> state.
    /// </exception>
    public void End()
    {
        if (Status != GameStatus.InProgress)
            throw new DomainException(
                $"Game can only be ended from {GameStatus.InProgress} state. Current status: {Status}.");

        Status = GameStatus.Finished;
        CurrentPhase = null;

        var scoreOne = _scores[PlayerOne];
        var scoreTwo = _scores[PlayerTwo];

        var isDraw = scoreOne == scoreTwo;
        Guid? winnerId = isDraw
            ? null
            : scoreOne > scoreTwo ? PlayerOne : PlayerTwo;

        _domainEvents.Add(new GameEnded(Id, scoreOne, scoreTwo, winnerId, isDraw, DateTimeOffset.UtcNow));
    }

    // ── Board piece tracking ──────────────────────────────────────────────────

    /// <summary>
    /// Records that <paramref name="playerId"/> has placed a piece on the board.
    /// </summary>
    /// <exception cref="DomainException">
    /// Thrown when the player already has <see cref="MaxPiecesOnBoard"/> pieces on the board,
    /// or when <paramref name="playerId"/> is not a participant.
    /// </exception>
    public void AddPieceToBoard(Guid playerId)
    {
        if (Status != GameStatus.InProgress)
            throw new DomainException(
                $"Pieces can only be tracked while the game is {GameStatus.InProgress}. Current status: {Status}.");

        if (!_piecesOnBoard.TryGetValue(playerId, out var nrOfPiecesOnBoard))
            throw new DomainException($"Player {playerId} is not a participant of game {Id}.");

        if (nrOfPiecesOnBoard >= MaxPiecesOnBoard)
            throw new DomainException(
                $"Player {playerId} already has the maximum of {MaxPiecesOnBoard} pieces on the board.");

        _piecesOnBoard[playerId]++;
    }

    /// <summary>
    /// Records that one of <paramref name="playerId"/>'s pieces has been removed from the board.
    /// </summary>
    /// <exception cref="DomainException">
    /// Thrown when <paramref name="playerId"/> is not a participant,
    /// or when the player has no pieces on the board to remove.
    /// </exception>
    public void RemovePieceFromBoard(Guid playerId)
    {
        if (Status != GameStatus.InProgress)
            throw new DomainException(
                $"Pieces can only be tracked while the game is {GameStatus.InProgress}. Current status: {Status}.");

        if (!_piecesOnBoard.TryGetValue(playerId, out var nrOfPiecesOnBoard))
            throw new DomainException($"Player {playerId} is not a participant of game {Id}.");

        if (nrOfPiecesOnBoard <= 0)
            throw new DomainException(
                $"Player {playerId} has no pieces on the board to remove.");

        _piecesOnBoard[playerId]--;
    }

    // ── Turn advancement ──────────────────────────────────────────────────────

    /// <summary>
    /// Advances to the next turn. If the current turn is the 5th (final) turn,
    /// <see cref="End"/> is called automatically.
    /// </summary>
    /// <exception cref="DomainException">
    /// Thrown when the game is not <see cref="GameStatus.InProgress"/>.
    /// </exception>
    public void AdvanceTurn()
    {
        if (Status != GameStatus.InProgress)
            throw new DomainException(
                $"Turn can only advance while the game is {GameStatus.InProgress}. Current status: {Status}.");

        EnsureInMovePhase();
        AdvancePhase(); // handles turn increment, game-end, and TurnPhaseAdvanced event
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Returns the current score for <paramref name="playerId"/>.</summary>
    /// <exception cref="DomainException">
    /// Thrown when <paramref name="playerId"/> is not a participant of this game.
    /// </exception>
    public int GetScore(Guid playerId) =>
        !_scores.TryGetValue(playerId, out var score) ?
            throw new DomainException($"Player {playerId} is not a participant of game {Id}.") : score;

    /// <summary>
    /// Returns the number of pieces <paramref name="playerId"/> currently has on the board.
    /// </summary>
    /// <exception cref="DomainException">
    /// Thrown when <paramref name="playerId"/> is not a participant of this game.
    /// </exception>
    public int GetPiecesOnBoardCount(Guid playerId) =>
        !_piecesOnBoard.TryGetValue(playerId, out var count) ?
            throw new DomainException($"Player {playerId} is not a participant of game {Id}.") : count;
}
