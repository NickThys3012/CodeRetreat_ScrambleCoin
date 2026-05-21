using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Events;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Entities;

/// <summary>
/// Turn and phase management logic.
/// Handles phase advancement, phase-specific actions (CoinSpawn, PlacePhase, MovePhase),
/// and phase guard methods.
/// </summary>
public partial class Game
{
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
            default:
                throw new DomainException($"Current phase isn't implemented: {CurrentPhase}");
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

    // ── Private helpers ───────────────────────────────────────────────────────

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

    private void MarkPlacePhaseActed(Guid playerId)
    {
        _placePhaseDone.Add(playerId);
        if (_placePhaseDone.Contains(PlayerOne) && _placePhaseDone.Contains(PlayerTwo))
            AdvancePhase(); // PlacePhase → MovePhase
    }

    private Lineup GetLineupForPlayer(Guid playerId)
    {
        if (playerId == PlayerOne)
            return LineupPlayerOne ?? throw new DomainException("PlayerOne's lineup has not been set.");
        return LineupPlayerTwo ?? throw new DomainException("PlayerTwo's lineup has not been set.");
    }
}
