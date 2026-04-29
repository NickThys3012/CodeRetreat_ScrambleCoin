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

    private readonly HashSet<Guid> _placePhaseDone = new HashSet<Guid>();

    // ── Move-phase tracking ───────────────────────────────────────────────────

    private readonly HashSet<Guid> _movePhaseDone = new HashSet<Guid>();

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

        if (board is null)
            throw new DomainException("Board must not be null.");

        Id = id;
        PlayerOne = playerOne;
        PlayerTwo = playerTwo;
        Board = board;
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

        bool isDraw = scoreOne == scoreTwo;
        Guid? winnerId = isDraw
            ? null
            : (scoreOne > scoreTwo ? PlayerOne : PlayerTwo);

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

        if (!_piecesOnBoard.ContainsKey(playerId))
            throw new DomainException($"Player {playerId} is not a participant of game {Id}.");

        if (_piecesOnBoard[playerId] >= MaxPiecesOnBoard)
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

        if (!_piecesOnBoard.ContainsKey(playerId))
            throw new DomainException($"Player {playerId} is not a participant of game {Id}.");

        if (_piecesOnBoard[playerId] <= 0)
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
                _movePhaseDone.Clear();
                _domainEvents.Add(new TurnPhaseAdvanced(Id, TurnNumber, previousPhase, CurrentPhase, DateTimeOffset.UtcNow));
                break;

            case TurnPhase.MovePhase:
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
    public int GetScore(Guid playerId)
    {
        if (!_scores.TryGetValue(playerId, out var score))
            throw new DomainException($"Player {playerId} is not a participant of game {Id}.");
        return score;
    }

    /// <summary>
    /// Returns the number of pieces <paramref name="playerId"/> currently has on the board.
    /// </summary>
    /// <exception cref="DomainException">
    /// Thrown when <paramref name="playerId"/> is not a participant of this game.
    /// </exception>
    public int GetPiecesOnBoardCount(Guid playerId)
    {
        if (!_piecesOnBoard.TryGetValue(playerId, out var count))
            throw new DomainException($"Player {playerId} is not a participant of game {Id}.");
        return count;
    }

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
            throw new DomainException($"Player {playerId} has already acted during the Place Phase this turn.");

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
        bool coinCollected = coin is not null;
        int coinValue = coin?.Value ?? 0;
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
    /// when <paramref name="existingPieceId"/> equals <paramref name="newPieceId"/>,
    /// when the target position violates the new piece's entry-point type,
    /// when the target tile is covered by an obstacle,
    /// or when the target tile is already occupied by a different piece.
    /// </exception>
    public void ReplacePiece(Guid playerId, Guid existingPieceId, Guid newPieceId, Position position)
    {
        EnsureInPlacePhase();
        EnsureIsParticipant(playerId);

        if (_placePhaseDone.Contains(playerId))
            throw new DomainException($"Player {playerId} has already acted during the Place Phase this turn.");

        if (existingPieceId == newPieceId)
            throw new DomainException("ExistingPieceId and NewPieceId must be different.");

        var lineup = GetLineupForPlayer(playerId);

        var existingPiece = lineup.Pieces.SingleOrDefault(p => p.Id == existingPieceId)
            ?? throw new DomainException($"Piece {existingPieceId} is not in player {playerId}'s lineup.");

        if (!existingPiece.IsOnBoard)
            throw new DomainException($"Piece {existingPieceId} is not on the board; cannot replace it.");

        var newPiece = lineup.Pieces.SingleOrDefault(p => p.Id == newPieceId)
            ?? throw new DomainException($"Piece {newPieceId} is not in player {playerId}'s lineup.");

        if (newPiece.IsOnBoard)
            throw new DomainException($"Piece {newPieceId} is already on the board; cannot use it as the replacement.");

        if (!Board.IsValidEntryPoint(position, newPiece.EntryPointType))
            throw new DomainException($"Position {position} is not a valid entry point for entry type {newPiece.EntryPointType}.");

        if (Board.IsObstacleCovering(position))
            throw new DomainException($"Cannot place piece at {position}: position is covered by an obstacle.");

        // Remove existing piece from its current tile so the tile.AsPiece check handles same-tile replacement correctly.
        var existingTile = Board.GetTile(existingPiece.Position!);
        existingTile.ClearOccupant();
        existingPiece.RemoveFromBoard();
        _piecesOnBoard[playerId]--;

        // Now check the target tile (may be the same tile — now clear after removal above).
        var tile = Board.GetTile(position);

        if (tile.AsPiece is not null)
        {
            // Undo the removal before throwing.
            existingPiece.PlaceAt(existingTile.Position);
            existingTile.SetOccupant(existingPiece);
            _piecesOnBoard[playerId]++;
            throw new DomainException($"Cannot place piece at {position}: tile is already occupied by another piece.");
        }

        // Collect coin if present at target tile.
        var coin = tile.AsCoin;
        bool coinCollected = coin is not null;
        int coinValue = coin?.Value ?? 0;
        if (coinCollected)
        {
            tile.ClearOccupant();
            _scores[playerId] += coinValue;
        }

        newPiece.PlaceAt(position);
        tile.SetOccupant(newPiece);
        _piecesOnBoard[playerId]++;

        _domainEvents.Add(new PieceReplaced(Id, TurnNumber, playerId, existingPieceId, newPieceId, position, coinCollected, coinValue, DateTimeOffset.UtcNow));

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
            throw new DomainException($"Player {playerId} has already acted during the Place Phase this turn.");

        MarkPlacePhaseActed(playerId);
    }

    // ── Piece movement ────────────────────────────────────────────────────────

    /// <summary>
    /// Applies all move actions for every on-board piece belonging to <paramref name="playerId"/>
    /// during MovePhase. Each entry in <paramref name="moves"/> maps a piece to its list of
    /// movement segments; every on-board piece must be included.
    /// </summary>
    /// <remarks>
    /// Rules enforced per-piece:
    /// <list type="bullet">
    ///   <item>Number of segments must equal <see cref="Piece.MovesPerTurn"/>.</item>
    ///   <item>Each segment must have 1 to <see cref="Piece.MaxDistance"/> steps (or 0 only when
    ///         no valid move exists, i.e., the piece is fully blocked).</item>
    ///   <item>Step adjacency must match <see cref="Piece.MovementType"/>.</item>
    ///   <item>Each step must be passable (no Rock/Lake/Fence blocking).</item>
    ///   <item>No step may land on a tile already occupied by a piece.</item>
    /// </list>
    /// Coins are collected on every tile the piece steps onto.
    /// Auto-advances to the next turn once both players have submitted.
    /// </remarks>
    /// <param name="playerId">The player submitting their moves.</param>
    /// <param name="moves">One entry per on-board piece: piece id and list of movement segments.</param>
    /// <exception cref="DomainException">
    /// Thrown when the current phase is not <see cref="TurnPhase.MovePhase"/>,
    /// when the player is not a participant or has already submitted moves this phase,
    /// when the moves list does not cover exactly the on-board pieces,
    /// or when any individual move violates the movement rules.
    /// </exception>
    public void MoveAllPieces(
        Guid playerId,
        IEnumerable<(Guid PieceId, IReadOnlyList<IReadOnlyList<Position>> Segments)> moves)
    {
        EnsureIsParticipant(playerId);
        EnsureInMovePhase();

        if (_movePhaseDone.Contains(playerId))
            throw new DomainException($"Player {playerId} has already submitted moves during the Move Phase this turn.");

        var lineup = GetLineupForPlayer(playerId);

        // Snapshot on-board pieces before processing.
        var onBoardPieces = lineup.Pieces.Where(p => p.IsOnBoard).ToList();

        var moveList = moves.ToList();

        // All on-board pieces must be covered — same count and same IDs.
        var movePieceIds = moveList.Select(m => m.PieceId).ToHashSet();
        var onBoardIds   = onBoardPieces.Select(p => p.Id).ToHashSet();

        if (moveList.Count != onBoardIds.Count || !movePieceIds.SetEquals(onBoardIds))
            throw new DomainException(
                $"The moves list must contain exactly one entry per on-board piece. " +
                $"Expected: [{string.Join(", ", onBoardIds)}]; Got: [{string.Join(", ", movePieceIds)}].");

        foreach (var (pieceId, segments) in moveList)
        {
            var piece = lineup.Pieces.Single(p => p.Id == pieceId);

            // piece.IsOnBoard is already guaranteed by the set-equality check above, but be explicit.
            if (!piece.IsOnBoard)
                throw new DomainException($"Piece {pieceId} is not on the board.");

            var startPosition = piece.Position!;
            var currentPosition = startPosition;

            // Check whether the piece has any valid first move (used to allow empty segments).
            var hasAnyValidMove = Board.HasAnyValidMove(currentPosition, piece.MovementType);

            // Validate segment count.
            if (segments.Count != piece.MovesPerTurn)
            {
                // Only exception: the piece is completely blocked and MovesPerTurn == 1
                // and the caller passes exactly 1 empty segment.
                bool allowedStuckException =
                    !hasAnyValidMove &&
                    piece.MovesPerTurn == 1 &&
                    segments.Count == 1 &&
                    segments[0].Count == 0;

                if (!allowedStuckException)
                    throw new DomainException(
                        $"Piece {pieceId} requires exactly {piece.MovesPerTurn} segment(s), but {segments.Count} were provided.");
            }

            var fullPath = new List<Position>();

            for (var segIndex = 0; segIndex < segments.Count; segIndex++)
            {
                var segment = segments[segIndex];

                if (segment.Count == 0)
                {
                    // An empty segment is only permitted when the piece has no valid move.
                    if (Board.HasAnyValidMove(currentPosition, piece.MovementType))
                        throw new DomainException(
                            $"Piece {pieceId}, segment {segIndex}: an empty segment is not allowed when a valid move exists.");
                    continue;
                }

                if (segment.Count > piece.MaxDistance)
                    throw new DomainException(
                        $"Piece {pieceId}, segment {segIndex}: segment has {segment.Count} step(s), but MaxDistance is {piece.MaxDistance}.");

                var segFrom = currentPosition;

                foreach (var stepTo in segment)
                {
                    // Validate step adjacency based on MovementType.
                    switch (piece.MovementType)
                    {
                        case MovementType.Orthogonal:
                            if (!segFrom.IsOrthogonallyAdjacentTo(stepTo))
                                throw new DomainException(
                                    $"Piece {pieceId}: step from {segFrom} to {stepTo} is not orthogonal.");
                            break;
                        case MovementType.Diagonal:
                            if (!segFrom.IsDiagonallyAdjacentTo(stepTo))
                                throw new DomainException(
                                    $"Piece {pieceId}: step from {segFrom} to {stepTo} is not diagonal.");
                            break;
                        case MovementType.AnyDirection:
                            if (!segFrom.IsOrthogonallyAdjacentTo(stepTo) && !segFrom.IsDiagonallyAdjacentTo(stepTo))
                                throw new DomainException(
                                    $"Piece {pieceId}: step from {segFrom} to {stepTo} is not adjacent.");
                            break;
                    }

                    // Passability (obstacles + fences).
                    if (!Board.IsPassable(segFrom, stepTo))
                        throw new DomainException(
                            $"Piece {pieceId}: step from {segFrom} to {stepTo} is blocked (obstacle or fence).");

                    // Target must not be occupied by a piece.
                    var targetTile = Board.GetTile(stepTo);
                    if (targetTile.AsPiece is not null)
                        throw new DomainException(
                            $"Piece {pieceId}: tile {stepTo} is already occupied by a piece.");

                    // Collect coin if present.
                    var coin = targetTile.AsCoin;
                    if (coin is not null)
                    {
                        targetTile.ClearOccupant();
                        AddScore(playerId, coin.Value);
                        _domainEvents.Add(new Events.CoinCollected(
                            Id, TurnNumber, playerId, pieceId, stepTo,
                            coin.CoinType, coin.Value, DateTimeOffset.UtcNow));
                    }

                    fullPath.Add(stepTo);
                    segFrom = stepTo;
                }

                currentPosition = segFrom;
            }

            // Move the piece on the board.
            var fromTile = Board.GetTile(startPosition);
            fromTile.ClearOccupant();

            piece.PlaceAt(currentPosition);

            var toTile = Board.GetTile(currentPosition);
            toTile.SetOccupant(piece);

            _domainEvents.Add(new Events.PieceMoved(
                Id, TurnNumber, playerId, pieceId,
                startPosition, currentPosition,
                fullPath.AsReadOnly(), DateTimeOffset.UtcNow));
        }

        _movePhaseDone.Add(playerId);

        // Auto-advance once both players have submitted.
        if (_movePhaseDone.Contains(PlayerOne) && _movePhaseDone.Contains(PlayerTwo))
            AdvanceTurn();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

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
}
