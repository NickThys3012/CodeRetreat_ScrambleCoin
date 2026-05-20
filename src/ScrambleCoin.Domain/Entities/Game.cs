using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Events;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Entities;

/// <summary>
/// Aggregate root for a Scramblecoin game session.
/// Owns the <see cref="Board"/>, both player identifiers, their lineups, scores,
/// a turn counter (1–5), the current game status and phase.
/// </summary>
public sealed class Game
{
    // ── Constants ─────────────────────────────────────────────────────────────

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

    // ── Scoring ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Increments <paramref name="playerId"/>'s score by <paramref name="points"/>.
    /// </summary>
    /// <exception cref="DomainException">
    /// Thrown when the game is not <see cref="GameStatus.InProgress"/>,
    /// when <paramref name="playerId"/> is not a participant,
    /// or when <paramref name="points"/> is negative.
    /// </exception>
    public void AddScore(Guid playerId, int points)
    {
        if (Status != GameStatus.InProgress)
            throw new DomainException(
                $"Scores can only be updated while the game is {GameStatus.InProgress}. Current status: {Status}.");

        if (points < 0)
            throw new DomainException($"Points must be non-negative, but was {points}.");

        if (!_scores.ContainsKey(playerId))
            throw new DomainException($"Player {playerId} is not a participant of game {Id}.");

        _scores[playerId] += points;
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

    // ── Phase advancement ─────────────────────────────────────────────────────

    /// <summary>
    /// Advances the current phase within the turn following the mandatory sequence:
    /// <see cref="TurnPhase.CoinSpawn"/> → <see cref="TurnPhase.PlacePhase"/> → <see cref="TurnPhase.MovePhase"/>.
    /// After <see cref="TurnPhase.MovePhase"/>:
    /// <list type="bullet">
    ///   <item>If the current turn is less than <see cref="TotalTurns"/>, the turn number
    ///         increments and the phase resets to <see cref="TurnPhase.CoinSpawn"/>.</item>
    ///   <item>If the current turn equals <see cref="TotalTurns"/>, <see cref="End"/> is called
    ///         automatically and the game transitions to <see cref="GameStatus.Finished"/>.</item>
    /// </list>
    /// Raises a <see cref="TurnPhaseAdvanced"/> domain event on every transition.
    /// </summary>
    /// <exception cref="DomainException">
    /// Thrown when the game is not <see cref="GameStatus.InProgress"/>.
    /// </exception>
    public void AdvancePhase()
    {
        if (Status != GameStatus.InProgress)
            throw new DomainException(
                $"Phase can only advance while the game is {GameStatus.InProgress}. Current status: {Status}.");

        var previousPhase = CurrentPhase!.Value;

        switch (CurrentPhase)
        {
            case TurnPhase.CoinSpawn:
                CurrentPhase = TurnPhase.PlacePhase;
                _placePhaseDone.Clear();
                _domainEvents.Add(new TurnPhaseAdvanced(Id, TurnNumber, previousPhase, CurrentPhase, DateTimeOffset.UtcNow));
                break;

            case TurnPhase.PlacePhase:
                CurrentPhase = TurnPhase.MovePhase;
                _movedPieceIds.Clear();
                MovePhaseActivePlayer = PlayerOne;
                _domainEvents.Add(new TurnPhaseAdvanced(Id, TurnNumber, previousPhase, CurrentPhase, DateTimeOffset.UtcNow));
                break;

            case TurnPhase.MovePhase:
                // Apply move-phase-end abilities (Issue #50)
                OnMovePhaseEnd();
                CheckForkyAutoRemoval();

                if (TurnNumber >= TotalTurns)
                {
                    // Raise the event before ending the game so listeners see the final transition.
                    _domainEvents.Add(new TurnPhaseAdvanced(Id, TurnNumber, previousPhase, null, DateTimeOffset.UtcNow));
                    End();
                }
                else
                {
                    var oldTurnNumber = TurnNumber;
                    TurnNumber++;
                    CurrentPhase = TurnPhase.CoinSpawn;
                    _domainEvents.Add(new TurnPhaseAdvanced(Id, oldTurnNumber, previousPhase, CurrentPhase, DateTimeOffset.UtcNow));

                    // Apply turn-start abilities (Issue #50)
                    OnTurnStart();
                }
                break;
        }
    }

    // ── Coin spawning ─────────────────────────────────────────────────────────

    /// <summary>
    /// Places coins on the board during the <see cref="TurnPhase.CoinSpawn"/> phase.
    /// Raises a <see cref="CoinsSpawned"/> domain event after all coins are placed.
    /// </summary>
    /// <param name="coins">The positions and coin types to spawn.</param>
    /// <exception cref="DomainException">
    /// Thrown when the current phase is not <see cref="TurnPhase.CoinSpawn"/>,
    /// or when any target tile is not empty.
    /// </exception>
    public void SpawnCoins(IEnumerable<(Position Position, CoinType CoinType)> coins)
    {
        EnsureInCoinSpawnPhase();

        var spawned = new List<(Position Position, CoinType CoinType)>();

        foreach (var (position, coinType) in coins)
        {
            var tile = Board.GetTile(position);

            if (Board.IsObstacleCovering(position))
                throw new DomainException($"Cannot spawn a coin at {position}: position is covered by an obstacle.");
            if (!tile.IsEmpty)
                throw new DomainException($"Cannot spawn a coin at {position}: tile is already occupied.");

            tile.SetOccupant(new Coin(coinType));
            spawned.Add((position, coinType));
        }

        _domainEvents.Add(new CoinsSpawned(Id, TurnNumber, spawned.AsReadOnly(), DateTimeOffset.UtcNow));
    }

    // ── Phase guard methods ───────────────────────────────────────────────────

    /// <summary>
    /// Asserts that the game is currently in the <see cref="TurnPhase.CoinSpawn"/> phase.
    /// Call this at the start of any operation that is only valid during coin spawning.
    /// </summary>
    /// <exception cref="DomainException">
    /// Thrown when the current phase is not <see cref="TurnPhase.CoinSpawn"/>.
    /// </exception>
    public void EnsureInCoinSpawnPhase()
    {
        if (CurrentPhase != TurnPhase.CoinSpawn)
            throw new DomainException(
                $"This action is only allowed during the {TurnPhase.CoinSpawn} phase. Current phase: {CurrentPhase?.ToString() ?? "None"}.");
    }

    /// <summary>
    /// Asserts that the game is currently in the <see cref="TurnPhase.PlacePhase"/>.
    /// Call this at the start of any placement operation.
    /// </summary>
    /// <exception cref="DomainException">
    /// Thrown when the current phase is not <see cref="TurnPhase.PlacePhase"/>.
    /// </exception>
    public void EnsureInPlacePhase()
    {
        if (CurrentPhase != TurnPhase.PlacePhase)
            throw new DomainException(
                $"This action is only allowed during the {TurnPhase.PlacePhase} phase. Current phase: {CurrentPhase?.ToString() ?? "None"}.");
    }

    /// <summary>
    /// Asserts that the game is currently in the <see cref="TurnPhase.MovePhase"/>.
    /// Call this at the start of any movement operation.
    /// </summary>
    /// <exception cref="DomainException">
    /// Thrown when the current phase is not <see cref="TurnPhase.MovePhase"/>.
    /// </exception>
    public void EnsureInMovePhase()
    {
        if (CurrentPhase != TurnPhase.MovePhase)
            throw new DomainException(
                $"This action is only allowed during the {TurnPhase.MovePhase} phase. Current phase: {CurrentPhase?.ToString() ?? "None"}.");
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

    // ── Piece placement ───────────────────────────────────────────────────────

    /// <summary>
    /// Places an off-board piece at a valid entry-point tile during PlacePhase.
    /// Collects any coin on the target tile and adds it to the player's score.
    /// Auto-advances to MovePhase once both players have acted (or skipped).
    /// </summary>
    /// <exception cref="DomainException">
    /// Thrown when the current phase is not <see cref="TurnPhase.PlacePhase"/>,
    /// when the player is not a participant, when the player has already acted this phase,
    /// when the piece is not in the player's lineup, when the piece is already on the board,
    /// when the target position violates the piece's entry-point type,
    /// when the target tile is covered by an obstacle,
    /// when the target tile is already occupied by another piece,
    /// or when the player already has the maximum number of pieces on the board.
    /// </exception>
    public void PlacePiece(Guid playerId, Guid pieceId, Position position)
    {
        EnsureInPlacePhase();
        EnsureIsParticipant(playerId);

        if (_placePhaseDone.Contains(playerId))
            throw new PlayerAlreadyActedException(playerId);

        var lineup = GetLineupForPlayer(playerId);
        var piece = lineup.Pieces.SingleOrDefault(p => p.Id == pieceId)
            ?? throw new DomainException($"Piece {pieceId} is not in player {playerId}'s lineup.");

        if (piece.IsOnBoard)
            throw new DomainException($"Piece {pieceId} is already on the board. Use ReplacePiece to swap it.");

        if (!Board.IsValidEntryPoint(position, piece.EntryPointType))
            throw new DomainException($"Position {position} is not a valid entry point for entry type {piece.EntryPointType}.");

        if (_piecesOnBoard[playerId] >= MaxPiecesOnBoard)
            throw new DomainException($"Player {playerId} already has the maximum of {MaxPiecesOnBoard} pieces on the board.");

        if (Board.IsObstacleCovering(position))
            throw new DomainException($"Cannot place piece at {position}: position is covered by an obstacle.");

        var tile = Board.GetTile(position);

        if (tile.AsPiece is not null)
            throw new DomainException($"Cannot place piece at {position}: tile is already occupied by another piece.");

        // Collect coin if present.
        var coin = tile.AsCoin;
        var coinCollected = coin is not null;
        var coinValue = coin?.Value ?? 0;
        if (coinCollected)
        {
            tile.ClearOccupant();
            _scores[playerId] += coinValue;
        }

        piece.PlaceAt(position);
        tile.SetOccupant(piece);
        _piecesOnBoard[playerId]++;

        _domainEvents.Add(new PiecePlaced(Id, TurnNumber, playerId, pieceId, position, coinCollected, coinValue, DateTimeOffset.UtcNow));

        MarkPlacePhaseActed(playerId);
    }

    /// <summary>
    /// Removes an on-board piece and places a different lineup piece at the given position during PlacePhase.
    /// Collects any coin on the target tile and adds it to the player's score.
    /// Auto-advances to MovePhase once both players have acted (or skipped).
    /// </summary>
    /// <exception cref="DomainException">
    /// Thrown when the current phase is not <see cref="TurnPhase.PlacePhase"/>,
    /// when the player is not a participant, when the player has already acted this phase,
    /// when either piece is not in the player's lineup,
    /// when the existing piece is not on the board,
    /// when the new piece is already on the board,
    /// or when <paramref name="existingPieceId"/> equals <paramref name="newPieceId"/>.
    /// </exception>
    public void ReplacePiece(Guid playerId, Guid existingPieceId, Guid newPieceId)
    {
        EnsureInPlacePhase();
        EnsureIsParticipant(playerId);

        if (_placePhaseDone.Contains(playerId))
            throw new PlayerAlreadyActedException(playerId);

        if (existingPieceId == newPieceId)
            throw new DomainException("ExistingPieceId and NewPieceId must be different.");

        var lineup = GetLineupForPlayer(playerId);

        var existingPiece = lineup.Pieces.SingleOrDefault(p => p.Id == existingPieceId)
            ?? throw new DomainException($"Piece {existingPieceId} is not in player {playerId}'s lineup.");

        if (!existingPiece.IsOnBoard)
            throw new DomainException($"Piece {existingPieceId} is not on the board; cannot replace it.");

        // The new piece always lands at the tile the old piece occupied.
        var targetPosition = existingPiece.Position!;

        var newPiece = lineup.Pieces.SingleOrDefault(p => p.Id == newPieceId)
            ?? throw new DomainException($"Piece {newPieceId} is not in player {playerId}'s lineup.");

        if (newPiece.IsOnBoard)
            throw new DomainException($"Piece {newPieceId} is already on the board; cannot use it as the replacement.");

        // Remove the existing piece — this clears the tile the new piece will occupy.
        var existingTile = Board.GetTile(targetPosition);
        existingTile.ClearOccupant();
        existingPiece.RemoveFromBoard();
        _piecesOnBoard[playerId]--;

        // The tile was occupied by the old piece, so it is now clear. No occupant check is needed.
        var tile = Board.GetTile(targetPosition);

        // Collect coin if present at the target tile.
        var coin = tile.AsCoin;
        var coinCollected = coin is not null;
        var coinValue = coin?.Value ?? 0;
        if (coinCollected)
        {
            tile.ClearOccupant();
            _scores[playerId] += coinValue;
        }

        newPiece.PlaceAt(targetPosition);
        tile.SetOccupant(newPiece);
        _piecesOnBoard[playerId]++;

        _domainEvents.Add(new PieceReplaced(Id, TurnNumber, playerId, existingPieceId, newPieceId, targetPosition, coinCollected, coinValue, DateTimeOffset.UtcNow));

        MarkPlacePhaseActed(playerId);
    }

    /// <summary>
    /// Skips the placement action for this player this turn.
    /// Auto-advances to MovePhase once both players have acted (or skipped).
    /// </summary>
    /// <exception cref="DomainException">
    /// Thrown when the current phase is not <see cref="TurnPhase.PlacePhase"/>,
    /// when the player is not a participant,
    /// or when the player has already acted this phase.
    /// </exception>
    public void SkipPlacement(Guid playerId)
    {
        EnsureInPlacePhase();
        EnsureIsParticipant(playerId);

        if (_placePhaseDone.Contains(playerId))
            throw new PlayerAlreadyActedException(playerId);

        MarkPlacePhaseActed(playerId);
    }

    // ── Piece movement ────────────────────────────────────────────────────────

    /// <summary>
    /// Moves a single on-board piece during MovePhase, applying direction validation,
    /// obstacle/fence blocking, and coin collection along the path.
    /// Auto-advances to the next turn (or ends the game) once all on-board pieces
    /// from both players have moved.
    /// </summary>
    /// <remarks>
    /// Rules enforced:
    /// <list type="bullet">
    ///   <item>Number of segments must equal <see cref="Piece.MovesPerTurn"/>.</item>
    ///   <item>Each segment must have 1 to <see cref="Piece.MaxDistance"/> steps (or 0 only when
    ///         no valid move exists, i.e., the piece is fully blocked).</item>
    ///   <item>Step adjacency must match <see cref="Piece.MovementType"/>.</item>
    ///   <item>Each step must be passable (no Rock/Lake/Fence blocking).</item>
    ///   <item>No step may land on a tile already occupied by a piece.</item>
    /// </list>
    /// Coins are collected on every tile the piece steps onto.
    /// </remarks>
    /// <param name="playerId">The player submitting the move.</param>
    /// <param name="pieceId">The piece to move.</param>
    /// <param name="segments">
    /// One segment per <c>MovesPerTurn</c>. Each segment is an ordered list of positions
    /// the piece steps through during that move action (not including the starting position).
    /// </param>
    /// <exception cref="DomainException">
    /// Thrown when the current phase is not <see cref="TurnPhase.MovePhase"/>,
    /// when the player is not a participant,
    /// when the piece has already moved this turn,
    /// when the piece is not in the player's lineup or not on the board,
    /// or when any individual move violates the movement rules.
    /// </exception>
    public void MovePiece(
        Guid playerId,
        Guid pieceId,
        IReadOnlyList<IReadOnlyList<Position>> segments)
    {
        EnsureIsParticipant(playerId);
        EnsureInMovePhase();

        // Skip the active player forward if they have no pieces left to move
        // (e.g. a player who placed no pieces this turn). Advances to the next
        // player or ends MovePhase if both are done.
        SkipActiveMoverIfNoPiecesRemaining();

        if (CurrentPhase != TurnPhase.MovePhase)
            throw new DomainException("MovePhase has already ended — all on-board pieces have been moved.");

        // Strict sequential: only the active player may submit moves.
        if (playerId != MovePhaseActivePlayer)
            throw new DomainException(
                $"It is not player {playerId}'s turn to move. " +
                $"Current active mover: {MovePhaseActivePlayer}.");

        if (_movedPieceIds.Contains(pieceId))
            throw new DomainException($"Piece {pieceId} has already moved this turn.");

        var lineup = GetLineupForPlayer(playerId);

        var piece = lineup.Pieces.SingleOrDefault(p => p.Id == pieceId)
            ?? throw new DomainException($"Piece {pieceId} is not in player {playerId}'s lineup.");

        if (!piece.IsOnBoard)
            throw new DomainException($"Piece {pieceId} is not on the board.");

        var startPosition = piece.Position!;
        var currentPosition = startPosition;

        // Check whether the piece has any valid first move (used to allow empty segments).
        var hasAnyValidMove = Board.HasAnyValidMove(piece);

        // Validate segment count.
        if (segments.Count != piece.MovesPerTurn)
        {
            // Only exception: the piece is completely blocked and MovesPerTurn == 1,
            // and the caller passes exactly 1 empty segment.
            var allowedStuckException =
                !hasAnyValidMove &&
                piece.MovesPerTurn == 1 &&
                segments is [{ Count: 0 }];

            if (!allowedStuckException)
                throw new DomainException(
                    $"Piece {pieceId} requires exactly {piece.MovesPerTurn} segment(s), but {segments.Count} were provided.");
        }

        var fullPath = new List<Position>();

        for (var segIndex = 0; segIndex < segments.Count; segIndex++)
        {
            var segment = segments[segIndex];
            
            // Get the per-segment movement type and max distance
            var segmentMovementType = piece.GetSegmentMovementType(segIndex);
            var segmentMaxDistance = piece.GetSegmentMaxDistance(segIndex);

            if (segment.Count == 0)
            {
                // An empty segment is only permitted when the piece has no valid move.
                var hasValidMoveAtCurrentPos = segmentMovementType == MovementType.Jump
                    ? Board.HasAnyValidMove(currentPosition, segmentMovementType, segmentMaxDistance)
                    : Board.HasAnyValidMove(currentPosition, segmentMovementType);

                if (hasValidMoveAtCurrentPos)
                    throw new DomainException(
                        $"Piece {pieceId}, segment {segIndex}: an empty segment is not allowed when a valid move exists.");
                continue;
            }

            switch (segmentMovementType)
            {
                // Special handling for Charge movement.
                // Charge moves are encoded as a single segment with 1 position (the first step).
                case MovementType.Charge when segment.Count != 1:
                    throw new DomainException(
                        $"Piece {pieceId}, segment {segIndex}: Charge movement requires exactly 1 position, but {segment.Count} were provided.");
                case MovementType.Charge:
                    {
                        var firstStep = segment[0];
                        var chargePath = ResolveChargePath(currentPosition, firstStep, pieceId, segmentMovementType, playerId);

                        // Even if the first step is blocked (empty path), the move is still performed.
                        fullPath.AddRange(chargePath);
                        var chargeEndPosition = chargePath.Count > 0 ? chargePath[^1] : currentPosition;

                        // Apply ice patch slide after the charge completes (if any)
                        // The slide counts as part of the charge but doesn't cause a re-charge
                        if (Board.HasIcePatch(chargeEndPosition))
                        {
                            // Determine the previous position in the charge path
                            // If the charge path has multiple steps, use the second-to-last; 
                            // otherwise use the starting position
                            var slidePreviousPosition = chargePath.Count > 1 
                                ? chargePath[^2] 
                                : currentPosition;
                            var slidePosition = ApplyIcePatchSlide(chargeEndPosition, slidePreviousPosition, playerId, pieceId);
                            if (slidePosition != chargeEndPosition)
                            {
                                // The piece slid to a new position; add it to the full path
                                fullPath.Add(slidePosition);
                                chargeEndPosition = slidePosition;
                            }
                        }

                        currentPosition = chargeEndPosition;
                        continue;
                    }
                
                // Special handling for Ethereal movement.
                // Ethereal moves are encoded as a sequence of steps (like Orthogonal/Diagonal).
                case MovementType.Ethereal:
                    {
                        if (segment.Count > segmentMaxDistance)
                            throw new DomainException(
                                $"Piece {pieceId}, segment {segIndex}: segment has {segment.Count} step(s), but MaxDistance is {segmentMaxDistance}.");

                        var segFrom = currentPosition;

                        foreach (var stepTo in segment)
                        {
                            // Ethereal is only valid as Any direction (orthogonal or diagonal)
                            if (!segFrom.IsOrthogonallyAdjacentTo(stepTo) && !segFrom.IsDiagonallyAdjacentTo(stepTo))
                                throw new DomainException(
                                    $"Piece {pieceId}: step from {segFrom} to {stepTo} is not adjacent.");

                            // Ethereal respects fences (but not occupants on intermediate tiles)
                            if (Board.IsFenceBlocked(segFrom, stepTo))
                                throw new DomainException(
                                    $"Piece {pieceId}: step from {segFrom} to {stepTo} is blocked by a fence.");

                            // Collect coin if present at intermediate tiles.
                            var stepTile = Board.GetTile(stepTo);
                            var coin = stepTile.AsCoin;
                            if (coin is not null)
                            {
                                stepTile.ClearOccupant();
                                AddScore(playerId, coin.Value);
                                _domainEvents.Add(new CoinCollected(
                                    Id, TurnNumber, playerId, pieceId, stepTo,
                                    coin.CoinType, coin.Value, DateTimeOffset.UtcNow));
                            }

                            fullPath.Add(stepTo);
                            var positionAfterStep = stepTo;

                            // Apply ice patch slide if the piece landed on an ice patch
                            if (Board.HasIcePatch(positionAfterStep))
                            {
                                var slidePosition = ApplyIcePatchSlide(positionAfterStep, segFrom, playerId, pieceId);
                                if (slidePosition != positionAfterStep)
                                {
                                    // The piece slid to a new position; add it to the full path
                                    fullPath.Add(slidePosition);
                                    positionAfterStep = slidePosition;
                                }
                            }

                            segFrom = positionAfterStep;
                        }

                        currentPosition = segFrom;
                        break;
                    }
                
                // For Jump movement, each segment should be a single destination position.
                // For other movement types, the segment is a sequence of steps.
                case MovementType.Jump when segment.Count != 1:
                    throw new DomainException(
                        $"Piece {pieceId}, segment {segIndex}: Jump movement requires exactly 1 destination position, but {segment.Count} were provided.");
                case MovementType.Jump:
                    {
                        var destination = segment[0];

                        // Validate direction constraints based on MovementType
                        var rowDiff = destination.Row - currentPosition.Row;
                        var colDiff = destination.Col - currentPosition.Col;

                        // Cannot jump to the same position
                        if (rowDiff == 0 && colDiff == 0)
                            throw new DomainException(
                                $"Piece {pieceId}" + $": jump destination must be different from the current position.");

                        // Validate direction constraint for this jump's movement type
                        ValidateJumpDirectionConstraint(pieceId, currentPosition, destination, rowDiff, colDiff, segmentMovementType);

                        // Calculate distance based on the direction of the jump
                        var distance = CalculateJumpDistance(currentPosition, destination, segmentMovementType);

                        if (distance > segmentMaxDistance)
                            throw new DomainException(
                                $"Piece {pieceId}: jump from {currentPosition} to {destination} is {distance} tiles, but MaxDistance is {segmentMaxDistance}.");

                        // Destination must not be occupied by an obstacle.
                        if (Board.IsObstacleCovering(destination))
                            throw new DomainException(
                                $"Piece {pieceId}: tile {destination} is occupied by an obstacle.");
                
                        var destinationTile = Board.GetTile(destination);
                        var targetPiece = destinationTile.AsPiece;

                        // Special handling for Scar: can land on opponent pieces to remove them
                        if (piece.Name.Equals("Scar", StringComparison.OrdinalIgnoreCase))
                        {
                            if (targetPiece is not null && targetPiece.PlayerId != playerId)
                            {
                                // Scar landing on opponent: remove opponent
                                destinationTile.ClearOccupant();
                                targetPiece.RemoveFromBoard();
                                _domainEvents.Add(new PieceRemoved(
                                    Id, TurnNumber, targetPiece.Id, pieceId,
                                    destination, DateTimeOffset.UtcNow));
                            }
                            else if (targetPiece is not null)
                            {
                                // Scar landing on ally: reject
                                throw new DomainException(
                                    $"Piece {pieceId}: cannot land on ally piece at {destination}.");
                            }
                        }
                        // Special handling for Daisy: can land on any piece to swap
                        else if (piece.Name.Equals("Daisy", StringComparison.OrdinalIgnoreCase))
                        {
                            if (targetPiece is not null)
                            {
                                // Daisy landing on any piece: swap positions
                                var daisyTile = Board.GetTile(currentPosition);
                                daisyTile.ClearOccupant();
                                destinationTile.ClearOccupant();

                                daisyTile.SetOccupant(targetPiece);
                                targetPiece.PlaceAt(currentPosition);

                                // destinationTile will be set to Daisy after this case
                                fullPath.Add(currentPosition); // Track the swap

                                // If opponent, steal 1 coin
                                if (targetPiece.PlayerId != playerId)
                                {
                                    var opponentId = targetPiece.PlayerId;
                                    if (_scores.TryGetValue(opponentId, out var currentScore) && currentScore > 0)
                                    {
                                        _scores[opponentId] -= 1;
                                        _scores[playerId] += 1;

                                        _domainEvents.Add(new CoinStolen(
                                            Id, TurnNumber, opponentId, playerId,
                                            pieceId, 1, DateTimeOffset.UtcNow));
                                    }
                                }

                                // Daisy ends at destination
                            }
                        }
                        // Normal Jump validation: destination must not have a piece
                        else if (targetPiece is not null)
                        {
                            throw new DomainException(
                                $"Piece {pieceId}: tile {destination} is already occupied by a piece.");
                        }

                        // Collect coin only at destination (not along the path).
                        var coin = destinationTile.AsCoin;
                        if (coin is not null)
                        {
                            destinationTile.ClearOccupant();
                            AddScore(playerId, coin.Value);
                            _domainEvents.Add(new CoinCollected(
                                Id, TurnNumber, playerId, pieceId, destination,
                                coin.CoinType, coin.Value, DateTimeOffset.UtcNow));
                        }

                        fullPath.Add(destination);
                        currentPosition = destination;
                        break;
                    }
                
                // For Orthogonal movement: step-by-step validation horizontally/vertically only.
                case MovementType.Orthogonal:
                    {
                        if (segment.Count > segmentMaxDistance)
                            throw new DomainException(
                                $"Piece {pieceId}, segment {segIndex}: segment has {segment.Count} step(s), but MaxDistance is {segmentMaxDistance}.");

                        var segFrom = currentPosition;

                        foreach (var stepTo in segment)
                        {
                            if (!segFrom.IsOrthogonallyAdjacentTo(stepTo))
                                throw new DomainException(
                                    $"Piece {pieceId}: step from {segFrom} to {stepTo} is not orthogonal.");

                            // Passability (obstacles + fences).
                            // Special handling for Stitch (Orthogonal-only): can pass through and destroy fences
                            var isStitch = piece.Name.Equals("Stitch", StringComparison.OrdinalIgnoreCase);
                            if (isStitch)
                            {
                                // For Stitch: check only for rocks and lakes, not fences
                                var hasRock = Board.HasRock(stepTo);
                                var hasLake = Board.IsObstacleCovering(stepTo); // This checks both rocks and lakes

                                if (hasRock || (hasLake && !Board.HasFence(stepTo)))
                                    throw new DomainException(
                                        $"Piece {pieceId}: step from {segFrom} to {stepTo} is blocked by a rock or lake.");

                                // If blocked by a fence, destroy it and continue
                                if (Board.HasFence(stepTo))
                                {
                                    Board.DestroyFence(stepTo);
                                    _domainEvents.Add(new FenceDestroyed(
                                        Id, TurnNumber, stepTo, DateTimeOffset.UtcNow));
                                }
                            }
                            else
                            {
                                // Normal passability check: cannot pass through obstacles or fences
                                if (Board.IsObstacleCovering(stepTo) || Board.IsFenceBlocked(segFrom, stepTo))
                                    throw new DomainException(
                                        $"Piece {pieceId}: step from {segFrom} to {stepTo} is blocked.");
                            }

                            // Check if tile is occupied by an opponent or ally piece (cannot land on pieces)
                            var stepTile = Board.GetTile(stepTo);
                            if (stepTile.AsPiece is not null)
                                throw new DomainException(
                                    $"Piece {pieceId}: step from {segFrom} to {stepTo} is occupied by a piece.");

                            // Collect coin if present
                            var coin = stepTile.AsCoin;
                            if (coin is not null)
                            {
                                stepTile.ClearOccupant();
                                AddScore(playerId, coin.Value);
                                _domainEvents.Add(new CoinCollected(
                                    Id, TurnNumber, playerId, pieceId, stepTo,
                                    coin.CoinType, coin.Value, DateTimeOffset.UtcNow));
                            }

                            fullPath.Add(stepTo);
                            var positionAfterStep = stepTo;

                            // Apply ice patch slide if the piece landed on an ice patch
                            if (Board.HasIcePatch(positionAfterStep))
                            {
                                var slidePosition = ApplyIcePatchSlide(positionAfterStep, segFrom, playerId, pieceId);
                                if (slidePosition != positionAfterStep)
                                {
                                    // The piece slid to a new position; add it to the full path
                                    fullPath.Add(slidePosition);
                                    positionAfterStep = slidePosition;
                                }
                            }

                            segFrom = positionAfterStep;
                        }

                        currentPosition = segFrom;
                        break;
                    }

                // For Diagonal movement: step-by-step validation diagonally only.
                case MovementType.Diagonal:
                    {
                        if (segment.Count > segmentMaxDistance)
                            throw new DomainException(
                                $"Piece {pieceId}, segment {segIndex}: segment has {segment.Count} step(s), but MaxDistance is {segmentMaxDistance}.");

                        var segFrom = currentPosition;

                        foreach (var stepTo in segment)
                        {
                            if (!segFrom.IsDiagonallyAdjacentTo(stepTo))
                                throw new DomainException(
                                    $"Piece {pieceId}: step from {segFrom} to {stepTo} is not diagonal.");

                            // Passability: cannot pass through obstacles or fences
                            if (Board.IsObstacleCovering(stepTo) || Board.IsFenceBlocked(segFrom, stepTo))
                                throw new DomainException(
                                    $"Piece {pieceId}: step from {segFrom} to {stepTo} is blocked.");

                            // Check if tile is occupied by a piece (cannot land on pieces)
                            var stepTile = Board.GetTile(stepTo);
                            if (stepTile.AsPiece is not null)
                                throw new DomainException(
                                    $"Piece {pieceId}: step from {segFrom} to {stepTo} is occupied by a piece.");

                            // Collect coin if present
                            var coin = stepTile.AsCoin;
                            if (coin is not null)
                            {
                                stepTile.ClearOccupant();
                                AddScore(playerId, coin.Value);
                                _domainEvents.Add(new CoinCollected(
                                    Id, TurnNumber, playerId, pieceId, stepTo,
                                    coin.CoinType, coin.Value, DateTimeOffset.UtcNow));
                            }

                            fullPath.Add(stepTo);
                            var positionAfterStep = stepTo;

                            // Apply ice patch slide if the piece landed on an ice patch
                            if (Board.HasIcePatch(positionAfterStep))
                            {
                                var slidePosition = ApplyIcePatchSlide(positionAfterStep, segFrom, playerId, pieceId);
                                if (slidePosition != positionAfterStep)
                                {
                                    // The piece slid to a new position; add it to the full path
                                    fullPath.Add(slidePosition);
                                    positionAfterStep = slidePosition;
                                }
                            }

                            segFrom = positionAfterStep;
                        }

                        currentPosition = segFrom;
                        break;
                    }

                // For AnyDirection movement: step-by-step validation orthogonally or diagonally.
                case MovementType.AnyDirection:
                    {
                        if (segment.Count > segmentMaxDistance)
                            throw new DomainException(
                                $"Piece {pieceId}, segment {segIndex}: segment has {segment.Count} step(s), but MaxDistance is {segmentMaxDistance}.");

                        var segFrom = currentPosition;

                        foreach (var stepTo in segment)
                        {
                            if (!segFrom.IsOrthogonallyAdjacentTo(stepTo) && !segFrom.IsDiagonallyAdjacentTo(stepTo))
                                throw new DomainException(
                                    $"Piece {pieceId}: step from {segFrom} to {stepTo} is not adjacent.");

                            // Passability: cannot pass through obstacles or fences
                            if (Board.IsObstacleCovering(stepTo) || Board.IsFenceBlocked(segFrom, stepTo))
                                throw new DomainException(
                                    $"Piece {pieceId}: step from {segFrom} to {stepTo} is blocked.");

                            // Check if tile is occupied by a piece (cannot land on pieces)
                            var stepTile = Board.GetTile(stepTo);
                            if (stepTile.AsPiece is not null)
                                throw new DomainException(
                                    $"Piece {pieceId}: step from {segFrom} to {stepTo} is occupied by a piece.");

                            // Collect coin if present
                            var coin = stepTile.AsCoin;
                            if (coin is not null)
                            {
                                stepTile.ClearOccupant();
                                AddScore(playerId, coin.Value);
                                _domainEvents.Add(new CoinCollected(
                                    Id, TurnNumber, playerId, pieceId, stepTo,
                                    coin.CoinType, coin.Value, DateTimeOffset.UtcNow));
                            }

                            fullPath.Add(stepTo);
                            var positionAfterStep = stepTo;

                            // Apply ice patch slide if the piece landed on an ice patch
                            if (Board.HasIcePatch(positionAfterStep))
                            {
                                var slidePosition = ApplyIcePatchSlide(positionAfterStep, segFrom, playerId, pieceId);
                                if (slidePosition != positionAfterStep)
                                {
                                    // The piece slid to a new position; add it to the full path
                                    fullPath.Add(slidePosition);
                                    positionAfterStep = slidePosition;
                                }
                            }

                            segFrom = positionAfterStep;
                        }

                        currentPosition = segFrom;
                        break;
                    }

                default:
                    throw new DomainException(
                        $"Unknown movement type: {segmentMovementType}. This should never happen.");
            }
        }

        // Special validation for Ethereal movement: destination must not have a piece occupant (only if moved).
        // Note: For multi-step pieces, only check the last segment's movement type
        var finalSegmentMovementType = piece.GetSegmentMovementType(segments.Count - 1);
        if (finalSegmentMovementType == MovementType.Ethereal && currentPosition != startPosition)
        {
            var destinationTile = Board.GetTile(currentPosition);
            if (destinationTile.AsPiece is not null)
                throw new DomainException(
                    $"Piece {pieceId}: tile {currentPosition} is already occupied by a piece. Ethereal movement must end on a free tile.");
            
            if (Board.IsObstacleCovering(currentPosition))
                throw new DomainException(
                    $"Piece {pieceId}: tile {currentPosition} is covered by an obstacle. Ethereal movement must end on a free tile.");
        }

        // Move the piece on the board.
        var fromTile = Board.GetTile(startPosition);
        fromTile.ClearOccupant();

        piece.PlaceAt(currentPosition);

        var toTile = Board.GetTile(currentPosition);
        toTile.SetOccupant(piece);

        _domainEvents.Add(new PieceMoved(
            Id, TurnNumber, playerId, pieceId,
            startPosition, currentPosition,
            fullPath.AsReadOnly(), DateTimeOffset.UtcNow));

        // If the piece is Elsa, place ice patches on all intermediate positions
        // (excluding start and destination).
        if (piece.IsElsa && startPosition != currentPosition)
        {
            PlaceElsaIcePatches(startPosition, fullPath);
        }

        // Execute on-stop abilities (Issue #49)
        ExecuteOnStopAbility(piece, playerId);

        // Execute passive abilities triggered by move (Issue #50)
        OnPieceMoved(playerId, piece, startPosition, currentPosition);

        _movedPieceIds.Add(pieceId);

        // Advance the active player when all their on-board pieces have moved.
        TryAutoAdvanceMovePhase();
    }

    /// <summary>
    /// Checks whether the current active mover in MovePhase has finished all their
    /// on-board piece moves. If so, switches to the other player (or advances the turn
    /// if both players are done). Handles the case where a player has no pieces on board.
    /// </summary>
    private void TryAutoAdvanceMovePhase()
    {
        if (MovePhaseActivePlayer is null) return;

        var activeLineup = MovePhaseActivePlayer == PlayerOne ? LineupPlayerOne! : LineupPlayerTwo!;
        var activePieceIds = activeLineup.Pieces.Where(p => p.IsOnBoard).Select(p => p.Id).ToHashSet();

        if (!activePieceIds.All(id => _movedPieceIds.Contains(id)))
            return;
        if (MovePhaseActivePlayer == PlayerOne)
        {
            MovePhaseActivePlayer = PlayerTwo;
            TryAutoAdvanceMovePhase(); // PlayerTwo may also have 0 pieces
        }
        else
        {
            MovePhaseActivePlayer = null;
            AdvanceTurn();
        }
    }

    /// <summary>
    /// Lazily skips the active mover forward when they have no remaining on-board pieces
    /// left to move (e.g. a player who placed no pieces during PlacePhase).
    /// Advances the active player — or ends MovePhase entirely — without requiring any
    /// explicit call from the caller.
    /// </summary>
    private void SkipActiveMoverIfNoPiecesRemaining()
    {
        while (MovePhaseActivePlayer is not null)
        {
            var lineup = MovePhaseActivePlayer == PlayerOne ? LineupPlayerOne! : LineupPlayerTwo!;
            var hasRemaining = lineup.Pieces.Any(p => p.IsOnBoard && !_movedPieceIds.Contains(p.Id));

            if (hasRemaining) break;

            if (MovePhaseActivePlayer == PlayerOne)
                MovePhaseActivePlayer = PlayerTwo;
            else
            {
                MovePhaseActivePlayer = null;
                AdvanceTurn();
                return;
            }
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Resolves a Charge movement by sliding from the starting position in the direction
    /// of the first step until hitting an obstacle, piece, or board edge.
    /// 
    /// The charge collects all coins along the path.
    /// If the first step is blocked, returns an empty list (piece doesn't move).
    /// </summary>
    /// <param name="startPosition">Current piece position.</param>
    /// <param name="firstStep">The first tile in the charge direction.</param>
    /// <param name="pieceId">ID of the moving piece (for error reporting).</param>
    /// <param name="movementType">The piece's movement type (Orthogonal, Diagonal, AnyDirection, or Charge).</param>
    /// <param name="playerId">The player collecting coins (for scoring).</param>
    /// <returns>
    /// A list of positions representing the full charge path (empty if the first step is blocked).
    /// </returns>
    private List<Position> ResolveChargePath(
        Position startPosition,
        Position firstStep,
        Guid pieceId,
        MovementType movementType,
        Guid playerId)
    {
        // Ensure the first step is adjacent to the start position.
        if (!startPosition.IsOrthogonallyAdjacentTo(firstStep) && !startPosition.IsDiagonallyAdjacentTo(firstStep))
            throw new DomainException(
                $"Piece {pieceId}: first charge step from {startPosition} to {firstStep} is not adjacent.");

        // Validate movement direction constraints.
        var isOrthogonal = startPosition.IsOrthogonallyAdjacentTo(firstStep);
        var isDiagonal = startPosition.IsDiagonallyAdjacentTo(firstStep);

        // For the Charge movement type, we allow both orthogonal and diagonal (the first step direction is unconstrained).
        // For other movement types, enforce the directional constraint.
        if (movementType != MovementType.Charge)
        {
            switch (movementType)
            {
                case MovementType.Orthogonal:
                    if (!isOrthogonal)
                        throw new DomainException(
                            $"Piece {pieceId}: charge from {startPosition} to {firstStep} is not orthogonal.");
                    break;
                case MovementType.Diagonal:
                    if (!isDiagonal)
                        throw new DomainException(
                            $"Piece {pieceId}: charge from {startPosition} to {firstStep} is not diagonal.");
                    break;
                case MovementType.AnyDirection:
                    // Any direction is allowed
                    break;
            }
        }

        // Check if the first step is blocked (obstacle, fence, or piece).
        if (!Board.IsPassable(startPosition, firstStep))
            return []; // First tile blocked: no movement

        var firstStepTile = Board.GetTile(firstStep);
        if (firstStepTile.AsPiece is not null)
            return []; // Piece blocking the first step: no movement

        // Determine the direction vector.
        var rowDelta = Math.Sign(firstStep.Row - startPosition.Row);
        var colDelta = Math.Sign(firstStep.Col - startPosition.Col);

        // Build the full charge path by sliding until blocked.
        var chargePath = new List<Position>();
        var currentPos = firstStep;

        while (true)
        {
            chargePath.Add(currentPos);

            // Collect coin if present.
            var currentTile = Board.GetTile(currentPos);
            var coin = currentTile.AsCoin;
            if (coin is not null)
            {
                currentTile.ClearOccupant();
                AddScore(playerId, coin.Value);
                _domainEvents.Add(new CoinCollected(
                    Id, TurnNumber, playerId, pieceId, currentPos,
                    coin.CoinType, coin.Value, DateTimeOffset.UtcNow));
            }

            // Try to move to the next tile in the charge direction.
            var nextRow = currentPos.Row + rowDelta;
            var nextCol = currentPos.Col + colDelta;

            // Check if the next position is out of bounds (board edge).
            if (nextRow < 0 || nextRow >= Board.Size || nextCol < 0 || nextCol >= Board.Size)
                break; // Hit the board edge: stop

            var nextPos = new Position(nextRow, nextCol);

            // Check if the next position is passable.
            if (!Board.IsPassable(currentPos, nextPos))
                break; // Obstacle or fence: stop

            // Check if the next position is occupied by a piece.
            var nextTile = Board.GetTile(nextPos);
            if (nextTile.AsPiece is not null)
                break; // Piece blocking: stop

            currentPos = nextPos;
        }

        return chargePath;
    }

    private void MarkPlacePhaseActed(Guid playerId)
    {
        _placePhaseDone.Add(playerId);
        if (_placePhaseDone.Contains(PlayerOne) && _placePhaseDone.Contains(PlayerTwo))
            AdvancePhase(); // PlacePhase → MovePhase
    }

    private void EnsureIsParticipant(Guid playerId)
    {
        if (playerId != PlayerOne && playerId != PlayerTwo)
            throw new DomainException($"Player {playerId} is not a participant of game {Id}.");
    }

    private Lineup GetLineupForPlayer(Guid playerId)
    {
        if (playerId == PlayerOne)
            return LineupPlayerOne ?? throw new DomainException("PlayerOne's lineup has not been set.");
        return LineupPlayerTwo ?? throw new DomainException("PlayerTwo's lineup has not been set.");
    }

    /// <summary>
    /// Applies an ice patch slide to a piece that has landed on an ice patch.
    /// 
    /// When a non-Jump piece lands on an ice patch, it slides one additional tile in the 
    /// same direction. If the slide is blocked by an obstacle, piece, or board edge, the 
    /// piece stops at its current position (no slide).
    /// 
    /// The slide:
    /// - Collects any coin on the slide destination tile
    /// - Does NOT count as part of a multi-move sequence (it's automatic/free)
    /// - Interacts with Charge (may cause early termination if blocked)
    /// - Is never applied to Jump pieces
    /// </summary>
    /// <param name="currentPosition">Current position of the piece (on the ice patch).</param>
    /// <param name="previousPosition">Position before landing on the ice patch (used to calculate direction).</param>
    /// <param name="playerId">Player collecting coins during the slide.</param>
    /// <param name="pieceId">ID of the sliding piece (for error reporting).</param>
    /// <returns>The position after the slide (may be the same as currentPosition if blocked).</returns>
    private Position ApplyIcePatchSlide(Position currentPosition, Position previousPosition, Guid playerId, Guid pieceId)
    {
        // Calculate the direction vector from previous to current position
        var rowDelta = Math.Sign(currentPosition.Row - previousPosition.Row);
        var colDelta = Math.Sign(currentPosition.Col - previousPosition.Col);
        
        // Calculate the target position for the slide
        var slideRow = currentPosition.Row + rowDelta;
        var slideCol = currentPosition.Col + colDelta;
        
        // Check if the slide would go out of bounds
        if (slideRow < 0 || slideRow >= Board.Size || slideCol < 0 || slideCol >= Board.Size)
            return currentPosition; // Out of bounds: blocked
        
        var slideTarget = new Position(slideRow, slideCol);
        
        // Check if the slide is blocked by a fence
        if (!Board.IsPassable(currentPosition, slideTarget))
            return currentPosition; // Blocked by fence or obstacle
        
        // Check if the slide target is occupied by a piece
        var slideTile = Board.GetTile(slideTarget);
        if (slideTile.AsPiece is not null)
            return currentPosition; // Blocked by piece
        
        // The slide is valid. Collect any coin at the slide destination.
        var coin = slideTile.AsCoin;
        if (coin is not null)
        {
            slideTile.ClearOccupant();
            AddScore(playerId, coin.Value);
            _domainEvents.Add(new CoinCollected(
                Id, TurnNumber, playerId, pieceId, slideTarget,
                coin.CoinType, coin.Value, DateTimeOffset.UtcNow));
        }
        
        return slideTarget;
    }

    /// <summary>
    /// Places ice patches on all intermediate positions that Elsa passed through.
    /// Excludes the starting position and the final destination.
    /// </summary>
    private void PlaceElsaIcePatches(Position startPosition, IReadOnlyList<Position> fullPath)
    {
        // Ice patches are placed on all positions except the final destination.
        // fullPath contains the visited positions in order (not including the starting position).
        for (var i = 0; i < fullPath.Count - 1; i++)
        {
            Board.PlaceIcePatch(fullPath[i]);
        }
    }

    /// <summary>
    /// Calculates the jump distance from <paramref name="from"/> to <paramref name="to"/> based on the
    /// <paramref name="movementType"/>. For Jump pieces, distance determines if the jump is valid
    /// within MaxDistance.
    /// 
    /// Distance is always Chebyshev distance (king move distance / max of |Δrow|, |Δcol|),
    /// which represents the number of tiles to reach the destination:
    /// - Orthogonal jump to (0,5) = 5 tiles
    /// - Diagonal jump to (3,3) = 3 tiles
    /// - AnyDirection jump to (2,3) = 3 tiles (max of 2 and 3)
    /// </summary>
    /// <exception cref="DomainException">
    /// Thrown if <paramref name="movementType"/> is not a valid Jump-compatible type.
    /// </exception>
    private static int CalculateJumpDistance(Position from, Position to, MovementType movementType)
    {
        // All Jump movement types use Chebyshev distance (max of |Δrow|, |Δcol|)
        return to.ChebyshevDistance(from);
    }

    // ── On-Stop Abilities (Issue #49) ─────────────────────────────────────────

    /// <summary>
    /// Executes the on-stop ability for a piece, if one exists.
    /// Piece identity is determined by <see cref="Piece.Name"/>.
    /// </summary>
    private void ExecuteOnStopAbility(Piece piece, Guid playerId)
    {
        switch (piece.Name)
        {
            case "Ralph":
                ApplyRalphAbility(piece);
                break;
            case "Pumbaa":
                ApplyPumbaaAbility(piece);
                break;
            case "WALL•E":
                ApplyWallEAbility(piece);
                break;
            case "Sulley":
                ApplySulleyAbility(piece, playerId);
                break;
            case "Rafiki":
                ApplyRafikiAbility(piece);
                break;
            // Note: Scar and Daisy abilities are handled during Jump resolution in MovePiece
            // Stitch ability is handled during movement (in the default case for Orthogonal)
        }
    }

    /// <summary>
    /// Ralph ability: Destroys all adjacent fences and rocks when Ralph stops.
    /// Raises RockDestroyed and FenceDestroyed events for each destroyed obstacle.
    /// </summary>
    private void ApplyRalphAbility(Piece piece)
    {
        var position = piece.Position!;
        var adjacentPositions = Board.GetOrthogonallyAdjacentPositions(position);

        foreach (var adjacent in adjacentPositions)
        {
            // Destroy rocks
            if (Board.DestroyRock(adjacent))
            {
                _domainEvents.Add(new RockDestroyed(
                    Id, TurnNumber, adjacent, DateTimeOffset.UtcNow));
            }

            // Destroy fences
            var fencesDestroyed = Board.DestroyFence(adjacent);
            for (int i = 0; i < fencesDestroyed; i++)
            {
                _domainEvents.Add(new FenceDestroyed(
                    Id, TurnNumber, adjacent, DateTimeOffset.UtcNow));
            }
        }
    }

    /// <summary>
    /// Pumbaa ability: Destroys all surrounding fences (not rocks) when Pumbaa stops.
    /// Raises FenceDestroyed event for each destroyed fence.
    /// </summary>
    private void ApplyPumbaaAbility(Piece piece)
    {
        var position = piece.Position!;
        var adjacentPositions = Board.GetAllAdjacentPositions(position);

        foreach (var adjacent in adjacentPositions)
        {
            // Destroy fences (not rocks)
            var fencesDestroyed = Board.DestroyFence(adjacent);
            for (int i = 0; i < fencesDestroyed; i++)
            {
                _domainEvents.Add(new FenceDestroyed(
                    Id, TurnNumber, adjacent, DateTimeOffset.UtcNow));
            }
        }
    }

    /// <summary>
    /// WALL•E ability: Pushes each adjacent piece 1 tile away from WALL•E.
    /// If a push is blocked by an edge or obstacle, the piece stays in place.
    /// Raises PieceMoved event for each successfully pushed piece.
    /// </summary>
    private void ApplyWallEAbility(Piece piece)
    {
        var position = piece.Position!;
        var adjacentPositions = Board.GetOrthogonallyAdjacentPositions(position);

        foreach (var adjacent in adjacentPositions)
        {
            var tile = Board.GetTile(adjacent);
            if (tile.AsPiece is null)
                continue; // No piece to push

            var targetPiece = tile.AsPiece;
            var pushDirection = GetPushDirection(position, adjacent);
            var pushTarget = GetPositionInDirection(adjacent, pushDirection);

            // Check if push target is valid (in bounds, no obstacle, no piece)
            if (pushTarget is not null &&
                !Board.IsObstacleCovering(pushTarget) &&
                Board.GetTile(pushTarget).AsPiece is null)
            {
                // Valid push - move the piece
                tile.ClearOccupant();
                var targetTile = Board.GetTile(pushTarget);
                targetTile.SetOccupant(targetPiece);
                targetPiece.PlaceAt(pushTarget);

                _domainEvents.Add(new PieceMoved(
                    Id, TurnNumber, targetPiece.PlayerId, targetPiece.Id,
                    adjacent, pushTarget, new[] { pushTarget }.ToList().AsReadOnly(),
                    DateTimeOffset.UtcNow));
            }
            // If blocked, piece stays in place (no event)
        }
    }

    /// <summary>
    /// Sulley ability: Pushes each adjacent opponent piece 2 tiles away.
    /// If the push path is blocked at 1st tile, piece stays in place.
    /// If the push path is blocked at 2nd tile, piece stops at 1st tile.
    /// Raises PieceMoved event for each successfully pushed piece.
    /// </summary>
    private void ApplySulleyAbility(Piece piece, Guid sulleyPlayerId)
    {
        var position = piece.Position!;
        var adjacentPositions = Board.GetOrthogonallyAdjacentPositions(position);

        foreach (var adjacent in adjacentPositions)
        {
            var tile = Board.GetTile(adjacent);
            if (tile.AsPiece is null)
                continue; // No piece to push

            var targetPiece = tile.AsPiece;

            // Only push opponent pieces
            if (targetPiece.PlayerId == sulleyPlayerId)
                continue;

            var pushDirection = GetPushDirection(position, adjacent);
            var pushTarget1 = GetPositionInDirection(adjacent, pushDirection);

            if (pushTarget1 is null)
                continue; // Can't push outside board

            // Check if first tile is blocked
            if (Board.IsObstacleCovering(pushTarget1) ||
                Board.GetTile(pushTarget1).AsPiece is not null)
                continue; // Can't push - first tile blocked

            // Try to push 2 tiles
            var pushTarget2 = GetPositionInDirection(pushTarget1, pushDirection);

            Position finalPushTarget;
            if (pushTarget2 is not null &&
                !Board.IsObstacleCovering(pushTarget2) &&
                Board.GetTile(pushTarget2).AsPiece is null)
            {
                // Can push 2 tiles
                finalPushTarget = pushTarget2;
            }
            else
            {
                // Can only push 1 tile
                finalPushTarget = pushTarget1;
            }

            // Move the piece
            tile.ClearOccupant();
            var finalTile = Board.GetTile(finalPushTarget);
            finalTile.SetOccupant(targetPiece);
            targetPiece.PlaceAt(finalPushTarget);

            _domainEvents.Add(new PieceMoved(
                Id, TurnNumber, targetPiece.PlayerId, targetPiece.Id,
                adjacent, finalPushTarget, new[] { finalPushTarget }.ToList().AsReadOnly(),
                DateTimeOffset.UtcNow));
        }
    }

    /// <summary>
    /// Rafiki ability: Pushes ALL adjacent pieces (ally and opponent) 1 tile away from Rafiki.
    /// If a push is blocked by an edge or obstacle, the piece stays in place.
    /// Raises PieceMoved event for each successfully pushed piece.
    /// </summary>
    private void ApplyRafikiAbility(Piece piece)
    {
        var position = piece.Position!;
        var adjacentPositions = Board.GetAllAdjacentPositions(position);

        foreach (var adjacent in adjacentPositions)
        {
            var tile = Board.GetTile(adjacent);
            if (tile.AsPiece is null)
                continue; // No piece to push

            var targetPiece = tile.AsPiece;
            var pushDirection = GetPushDirection(position, adjacent);
            var pushTarget = GetPositionInDirection(adjacent, pushDirection);

            // Check if push target is valid (in bounds, no obstacle, no piece)
            if (pushTarget is not null &&
                !Board.IsObstacleCovering(pushTarget) &&
                Board.GetTile(pushTarget).AsPiece is null)
            {
                // Valid push - move the piece
                tile.ClearOccupant();
                var targetTile = Board.GetTile(pushTarget);
                targetTile.SetOccupant(targetPiece);
                targetPiece.PlaceAt(pushTarget);

                _domainEvents.Add(new PieceMoved(
                    Id, TurnNumber, targetPiece.PlayerId, targetPiece.Id,
                    adjacent, pushTarget, new[] { pushTarget }.ToList().AsReadOnly(),
                    DateTimeOffset.UtcNow));
            }
            // If blocked, piece stays in place (no event)
        }
    }

    /// <summary>
    /// Scar ability (Jump): Landing on an opponent piece removes them.
    /// Landing on an ally or empty tile behaves as normal jump.
    /// This method should be called DURING jump resolution after the destination is determined.
    /// </summary>
    /// <remarks>
    /// This method is called from within the Jump case of MovePiece.
    /// Returns the final destination (same as input if no removal occurs).
    /// </remarks>
    public Position ApplyScarAbility(Position destination, Guid scarPlayerId)
    {
        var destinationTile = Board.GetTile(destination);
        var targetPiece = destinationTile.AsPiece;

        if (targetPiece is not null && targetPiece.PlayerId != scarPlayerId)
        {
            // Opponent piece - remove it
            destinationTile.ClearOccupant();
            targetPiece.RemoveFromBoard();

            _domainEvents.Add(new PieceRemoved(
                Id, TurnNumber, targetPiece.Id, /* scarPieceId would be passed separately */
                Guid.Empty, // Placeholder - caller will have this
                destination, DateTimeOffset.UtcNow));
        }

        return destination;
    }

    /// <summary>
    /// Daisy ability (Jump): Landing on any piece swaps positions.
    /// If the piece is an opponent, steals 1 coin.
    /// This method should be called DURING jump resolution after the destination is determined.
    /// </summary>
    /// <remarks>
    /// This method is called from within the Jump case of MovePiece.
    /// Returns the final destination (same as input if no swap occurs).
    /// </remarks>
    public Position ApplyDaisyAbility(Position daisyPosition, Position destination, Guid daisyPlayerId)
    {
        var destinationTile = Board.GetTile(destination);
        var targetPiece = destinationTile.AsPiece;

        if (targetPiece is not null)
        {
            // Swap positions
            var daisyTile = Board.GetTile(daisyPosition);
            daisyTile.ClearOccupant();
            destinationTile.ClearOccupant();

            daisyTile.SetOccupant(targetPiece);
            targetPiece.PlaceAt(daisyPosition);

            destinationTile.SetOccupant(null); // Will be set to Daisy by caller

            // If opponent, steal 1 coin
            if (targetPiece.PlayerId != daisyPlayerId)
            {
                var opponentId = targetPiece.PlayerId;
                var daisyOwner = daisyPlayerId;

                // Steal 1 coin
                if (_scores.TryGetValue(opponentId, out var currentScore) && currentScore > 0)
                {
                    _scores[opponentId] -= 1;
                    _scores[daisyOwner] += 1;

                    _domainEvents.Add(new CoinStolen(
                        Id, TurnNumber, opponentId, daisyOwner,
                        Guid.Empty, // Placeholder - caller will have Daisy's piece ID
                        1, DateTimeOffset.UtcNow));
                }
            }
        }

        return destination;
    }

    /// <summary>
    /// Stitch ability (Orthogonal): Can pass through and destroy fences along his movement path.
    /// This method should be called DURING orthogonal movement for each step.
    /// </summary>
    /// <remarks>
    /// This method is called from within the default (Orthogonal) case of MovePiece.
    /// Returns true if the step was allowed (either passable or fence destroyed).
    /// </remarks>
    public bool ApplyStitchAbility(Position from, Position to)
    {
        // Check if the move is blocked by a fence
        if (Board.IsFenceBlocked(from, to))
        {
            // Destroy all fences on the edge
            var fencesDestroyed = Board.DestroyFence(from);
            for (int i = 0; i < fencesDestroyed; i++)
            {
                _domainEvents.Add(new FenceDestroyed(
                    Id, TurnNumber, from, DateTimeOffset.UtcNow));
            }
            fencesDestroyed = Board.DestroyFence(to);
            for (int i = 0; i < fencesDestroyed; i++)
            {
                _domainEvents.Add(new FenceDestroyed(
                    Id, TurnNumber, to, DateTimeOffset.UtcNow));
            }

            return true; // Allow the move despite the fence
        }

        return false; // Move was passable without needing the ability
    }

    /// <summary>
    /// Helper: Calculates the push direction (away from source, towards target).
    /// Returns a direction vector (dr, dc) where each component is -1, 0, or 1.
    /// </summary>
    private static (int dr, int dc) GetPushDirection(Position source, Position target)
    {
        var dr = target.Row - source.Row;
        var dc = target.Col - source.Col;

        // Normalize to -1, 0, or 1
        if (dr != 0) dr = dr > 0 ? 1 : -1;
        if (dc != 0) dc = dc > 0 ? 1 : -1;

        return (dr, dc);
    }

    /// <summary>
    /// Helper: Calculates the position one tile in the given direction from the source.
    /// Returns null if the result would be out of bounds.
    /// </summary>
    private static Position? GetPositionInDirection(Position source, (int dr, int dc) direction)
    {
        var newRow = source.Row + direction.dr;
        var newCol = source.Col + direction.dc;

        if (newRow < 0 || newRow >= Board.Size || newCol < 0 || newCol >= Board.Size)
            return null;

        return new Position(newRow, newCol);
    }

    // ── Passive ability implementations (Issue #50) ────────────────────────────────

    /// <summary>
    /// Triggers at the start of each new turn (when transitioning to CoinSpawn phase for a new turn).
    /// Applies abilities like Moana (increase MaxDistance), Jafar (increase MovesPerTurn),
    /// and Cinderella (auto-remove at turn 5).
    /// </summary>
    private void OnTurnStart()
    {
        // Moana & Jafar: Increase stats each turn starting from turn 2.
        if (TurnNumber >= 2)
        {
            foreach (var playerId in new[] { PlayerOne, PlayerTwo })
            {
                var lineup = playerId == PlayerOne ? LineupPlayerOne! : LineupPlayerTwo!;
                foreach (var piece in lineup.Pieces)
                {
                    if (piece.IsOnBoard && piece.Name.Equals("Moana", StringComparison.OrdinalIgnoreCase))
                    {
                        piece.IncreaseMaxDistance();
                    }
                    if (piece.IsOnBoard && piece.Name.Equals("Jafar", StringComparison.OrdinalIgnoreCase))
                    {
                        piece.IncreaseMovesPerTurn();
                    }
                }
            }
        }

        // Cinderella: Auto-remove at turn 5 start.
        if (TurnNumber == 5)
        {
            foreach (var playerId in new[] { PlayerOne, PlayerTwo })
            {
                var lineup = playerId == PlayerOne ? LineupPlayerOne! : LineupPlayerTwo!;
                var cinderellas = lineup.Pieces.Where(p => p.IsOnBoard && p.Name.Equals("Cinderella", StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (var cinderella in cinderellas)
                {
                    // Save position BEFORE RemoveFromBoard() sets Position to null
                    var piecePosition = cinderella.Position!;
                    Board.GetTile(piecePosition).ClearOccupant();
                    RemovePieceFromBoard(playerId);
                    cinderella.RemoveFromBoard();

                    _domainEvents.Add(new PieceAutoRemoved(Id, TurnNumber, cinderella.Id, "Cinderella auto-removed at turn 5", DateTimeOffset.UtcNow));
                }
            }
        }

        // Reset temporary move adjustments for the new turn (Fairy Godmother / Ursula buffs).
        foreach (var playerId in new[] { PlayerOne, PlayerTwo })
        {
            var lineup = playerId == PlayerOne ? LineupPlayerOne! : LineupPlayerTwo!;
            foreach (var piece in lineup.Pieces)
            {
                piece.ResetTemporaryMoveAdjustment();
            }
        }
    }

    /// <summary>
    /// Triggers at the end of the MovePhase (before transitioning to next turn).
    /// Applies Scrooge ability: +1 coin to owning player per Scrooge on board.
    /// </summary>
    private void OnMovePhaseEnd()
    {
        // Scrooge: +1 bonus coin per Scrooge on board at end of turn.
        foreach (var playerId in new[] { PlayerOne, PlayerTwo })
        {
            var lineup = playerId == PlayerOne ? LineupPlayerOne! : LineupPlayerTwo!;
            var scroogeCount = lineup.Pieces.Count(p => p.IsOnBoard && p.Name.Equals("Scrooge", StringComparison.OrdinalIgnoreCase));

            if (scroogeCount > 0)
            {
                AddScore(playerId, scroogeCount);
                var scrooge = lineup.Pieces.First(p => p.IsOnBoard && p.Name.Equals("Scrooge", StringComparison.OrdinalIgnoreCase));
                _domainEvents.Add(new ScroogeGainedCoin(Id, TurnNumber, scrooge.Id, scroogeCount, DateTimeOffset.UtcNow));
            }
        }
    }

    /// <summary>
    /// Triggers after a piece moves (called from MovePiece).
    /// Applies abilities: Flynn (silver coin on previous tile), Merlin (convert silver to gold),
    /// Rapunzel (collect coins from adjacent tiles), Fairy Godmother (buff allies),
    /// Ursula (debuff opponents), Mike Wazowski (coin buff), Forky (track first move).
    /// </summary>
    private void OnPieceMoved(Guid playerId, Piece piece, Position fromPosition, Position toPosition)
    {
        // Flynn: Place silver coin on previous tile if empty.
        if (piece.Name.Equals("Flynn", StringComparison.OrdinalIgnoreCase))
        {
            var previousTile = Board.GetTile(fromPosition);
            if (previousTile.IsEmpty && !Board.IsObstacleCovering(fromPosition))
            {
                previousTile.SetOccupant(new Coin(CoinType.Silver));
            }
        }

        // Merlin: Convert nearest silver coin (≤2 tiles) to gold.
        if (piece.Name.Equals("Merlin", StringComparison.OrdinalIgnoreCase))
        {
            var nearestSilver = FindNearestSilverCoin(toPosition, maxDistance: 2);
            if (nearestSilver != null)
            {
                var tile = Board.GetTile(nearestSilver);
                if (tile.AsCoin?.CoinType == CoinType.Silver)
                {
                    tile.SetOccupant(new Coin(CoinType.Gold));
                    _domainEvents.Add(new CoinConverted(Id, TurnNumber, nearestSilver, "Silver", "Gold", DateTimeOffset.UtcNow));
                }
            }
        }

        // Rapunzel: Collect coins from up to 3 adjacent orthogonal tiles.
        if (piece.Name.Equals("Rapunzel", StringComparison.OrdinalIgnoreCase))
        {
            var adjacentPositions = Board.GetOrthogonallyAdjacentPositions(toPosition);
            var adjacentCoins = adjacentPositions
                .Where(pos => Board.GetTile(pos).AsCoin != null)
                .Take(3)
                .ToList();

            foreach (var coinPos in adjacentCoins)
            {
                var coin = Board.GetTile(coinPos).AsCoin;
                if (coin != null)
                {
                    AddScore(playerId, coin.CoinType == CoinType.Gold ? 3 : 1);
                    Board.GetTile(coinPos).ClearOccupant();
                }
            }
        }

        // Fairy Godmother: Give +1 move to adjacent ally pieces (this turn only).
        if (piece.Name.Equals("Fairy Godmother", StringComparison.OrdinalIgnoreCase))
        {
            ApplyMoveBuffToAllies(playerId, toPosition);
        }

        // Ursula: Give −1 move to adjacent opponent pieces (this turn only, min 0).
        if (piece.Name.Equals("Ursula", StringComparison.OrdinalIgnoreCase))
        {
            ApplyMoveDebuffToOpponents(playerId, toPosition);
        }

        // Mike Wazowski: Give random adjacent ally +1 coin on next collection.
        if (piece.Name.Equals("Mike Wazowski", StringComparison.OrdinalIgnoreCase))
        {
            ApplyCoinBuffToRandomAlly(playerId, toPosition);
        }

        // Forky: Track first move for auto-removal at end of first turn.
        if (piece.Name.Equals("Forky", StringComparison.OrdinalIgnoreCase))
        {
            piece.MarkAsMovedOnFirstTurn();
        }
    }

    /// <summary>
    /// Finds the nearest silver coin within maxDistance of the given position.
    /// Uses Chebyshev distance (max of absolute row and col differences).
    /// </summary>
    private Position? FindNearestSilverCoin(Position from, int maxDistance)
    {
        var coins = Board.GetAllCoins();
        var silverCoins = coins
            .Where(t => t.AsCoin?.CoinType == CoinType.Silver)
            .Where(t => from.ChebyshevDistance(t.Position) <= maxDistance)
            .OrderBy(t => from.ChebyshevDistance(t.Position))
            .FirstOrDefault();

        return silverCoins?.Position;
    }

    /// <summary>
    /// Applies +1 move adjustment to all adjacent ally pieces (temporary, this turn only).
    /// </summary>
    private void ApplyMoveBuffToAllies(Guid playerId, Position from)
    {
        var adjacentPositions = Board.GetAllAdjacentPositions(from);
        var affectedPieceIds = new List<Guid>();

        foreach (var adjPos in adjacentPositions)
        {
            var tile = Board.GetTile(adjPos);
            var adjPiece = tile.AsPiece;
            if (adjPiece != null && adjPiece.PlayerId == playerId && !adjPiece.Name.Equals("Fairy Godmother", StringComparison.OrdinalIgnoreCase))
            {
                adjPiece.ApplyTemporaryMoveAdjustment(1);
                affectedPieceIds.Add(adjPiece.Id);
            }
        }

        if (affectedPieceIds.Count > 0)
        {
            _domainEvents.Add(new MoveBuffApplied(Id, TurnNumber, Board.GetTile(from).AsPiece!.Id, affectedPieceIds.AsReadOnly(), 1, DateTimeOffset.UtcNow));
        }
    }

    /// <summary>
    /// Applies −1 move adjustment to all adjacent opponent pieces (temporary, this turn only, min 0).
    /// </summary>
    private void ApplyMoveDebuffToOpponents(Guid playerId, Position from)
    {
        var opponentId = playerId == PlayerOne ? PlayerTwo : PlayerOne;
        var adjacentPositions = Board.GetAllAdjacentPositions(from);
        var affectedPieceIds = new List<Guid>();

        foreach (var adjPos in adjacentPositions)
        {
            var tile = Board.GetTile(adjPos);
            var adjPiece = tile.AsPiece;
            if (adjPiece != null && adjPiece.PlayerId == opponentId && !adjPiece.Name.Equals("Ursula", StringComparison.OrdinalIgnoreCase))
            {
                adjPiece.ApplyTemporaryMoveAdjustment(-1);
                affectedPieceIds.Add(adjPiece.Id);
            }
        }

        if (affectedPieceIds.Count > 0)
        {
            _domainEvents.Add(new MoveDebuffApplied(Id, TurnNumber, Board.GetTile(from).AsPiece!.Id, affectedPieceIds.AsReadOnly(), 1, DateTimeOffset.UtcNow));
        }
    }

    /// <summary>
    /// Applies +1 coin buff to a random adjacent ally piece for their next coin collection.
    /// </summary>
    private void ApplyCoinBuffToRandomAlly(Guid playerId, Position from)
    {
        var adjacentPositions = Board.GetAllAdjacentPositions(from);
        var adjacentAllies = adjacentPositions
            .Select(p => Board.GetTile(p).AsPiece)
            .Where(p => p != null && p.PlayerId == playerId && !p.Name.Equals("Mike Wazowski", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (adjacentAllies.Count > 0)
        {
            // Use deterministic selection based on piece ID hash for testing consistency
            var selectedIndex = Math.Abs(HashCode.Combine(Id, TurnNumber, from).GetHashCode()) % adjacentAllies.Count;
            var randomAlly = adjacentAllies[selectedIndex];
            randomAlly.ApplyCoinBuff(1);
            _domainEvents.Add(new CoinBuffApplied(Id, TurnNumber, Board.GetTile(from).AsPiece!.Id, randomAlly.Id, 1, DateTimeOffset.UtcNow));
        }
    }

    /// <summary>
    /// Validates that a jump destination respects the piece's directional constraint.
    /// Jump pieces can have directional modifiers (Orthogonal-Jump, Diagonal-Jump, AnyDirection-Jump).
    /// </summary>
    private void ValidateJumpDirectionConstraint(
        Guid pieceId, Position from, Position to, int rowDiff, int colDiff, MovementType movementType)
    {
        switch (movementType)
        {
            case MovementType.Orthogonal:
                // Orthogonal-Jump: horizontal or vertical only
                if (rowDiff != 0 && colDiff != 0)
                    throw new DomainException(
                        $"Piece {pieceId}: jump from {from} to {to} is not orthogonal (must move only horizontally or vertically).");
                break;

            case MovementType.Diagonal:
                // Diagonal-Jump: equal row and column distance
                if (Math.Abs(rowDiff) != Math.Abs(colDiff))
                    throw new DomainException(
                        $"Piece {pieceId}: jump from {from} to {to} is not diagonal (must move equal rows and columns).");
                break;

            case MovementType.AnyDirection:
                // AnyDirection-Jump: no directional restriction
                break;

            case MovementType.Jump:
                // Pure Jump: can go in any direction
                break;
        }
    }

    /// <summary>
    /// Checks if Forky should be auto-removed at the end of the current turn move.
    /// Forky is removed after his first move turn (when he moves in a turn for the first time).
    /// This is called after MovePhase ends.
    /// </summary>
    private void CheckForkyAutoRemoval()
    {
        foreach (var playerId in new[] { PlayerOne, PlayerTwo })
        {
            var lineup = playerId == PlayerOne ? LineupPlayerOne! : LineupPlayerTwo!;
            var forky = lineup.Pieces.FirstOrDefault(p => p.IsOnBoard && p.Name.Equals("Forky", StringComparison.OrdinalIgnoreCase));
            
            if (forky != null && forky.HasMovedOnFirstTurn)
            {
                // Save position BEFORE RemoveFromBoard() sets Position to null
                var piecePosition = forky.Position!;
                Board.GetTile(piecePosition).ClearOccupant();
                RemovePieceFromBoard(playerId);
                forky.RemoveFromBoard();

                _domainEvents.Add(new PieceAutoRemoved(Id, TurnNumber, forky.Id, "Forky auto-removed after first move", DateTimeOffset.UtcNow));
            }
        }
    }
}
