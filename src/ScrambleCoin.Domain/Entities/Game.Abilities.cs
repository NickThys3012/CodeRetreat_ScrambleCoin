using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Events;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Entities;

/// <summary>
/// On-stop abilities and passive ability implementations.
/// Handles turn-based ability hooks and ability helper methods.
/// </summary>
public partial class Game
{
    // ── On-Stop Abilities (Issue #49) ─────────────────────────────────────────

    /// <summary>
    /// Executes the on-stop ability for a piece if one exists.
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
            for (var i = 0; i < fencesDestroyed; i++)
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
            for (var i = 0; i < fencesDestroyed; i++)
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

            // Check if the push target is valid (in bounds, no obstacle, no piece)
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
            // If blocked, a piece stays in place (no event)
        }
    }

    /// <summary>
    /// Sulley ability: Pushes each adjacent opponent piece 2 tiles away.
    /// If the push path is blocked at the 1st tile, a piece stays in place.
    /// If the push path is blocked at the 2nd tile, a piece stops at the 1st tile.
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

            // Check if the first tile is blocked
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

            // Check if the push target is valid (in bounds, no obstacle, no piece)
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
            // If blocked, a piece stays in place (no event)
        }
    }

    /// <summary>
    /// Scar ability (Jump): Landing on an opponent piece removes them.
    /// Landing on an ally or empty tile behaves as a normal jump.
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
            for (var i = 0; i < fencesDestroyed; i++)
            {
                _domainEvents.Add(new FenceDestroyed(
                    Id, TurnNumber, from, DateTimeOffset.UtcNow));
            }
            fencesDestroyed = Board.DestroyFence(to);
            for (var i = 0; i < fencesDestroyed; i++)
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
    /// Helper: Calculates the position of one tile in the given direction from the source.
    /// Returns null if the result is out of bounds.
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
    /// Triggers at the end of the MovePhase (before transitioning to the next turn).
    /// Applies Scrooge ability: +1 coin to the owning player per Scrooge on board.
    /// </summary>
    private void OnMovePhaseEnd()
    {
        // Scrooge: +1 bonus coin per Scrooge on board at the end of turn.
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
        // Flynn: Place a silver coin on the previous tile if empty.
        if (piece.Name.Equals("Flynn", StringComparison.OrdinalIgnoreCase))
        {
            var previousTile = Board.GetTile(fromPosition);
            if (previousTile.IsEmpty && !Board.IsObstacleCovering(fromPosition))
            {
                previousTile.SetOccupant(new Coin(CoinType.Silver));
            }
        }

        // Merlin: Convert the nearest silver coin (≤2 tiles) to gold.
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

        // Fairy Godmother: Give +1 move to adjacent allay pieces (this turn only).
        if (piece.Name.Equals("Fairy Godmother", StringComparison.OrdinalIgnoreCase))
        {
            ApplyMoveBuffToAllies(playerId, toPosition);
        }

        // Ursula: Give −1 move to adjacent opponent pieces (this turn only, min 0).
        if (piece.Name.Equals("Ursula", StringComparison.OrdinalIgnoreCase))
        {
            ApplyMoveDebuffToOpponents(playerId, toPosition);
        }

        // Mike Wazowski: Give random adjacent ally +1 coin on the next collection.
        if (piece.Name.Equals("Mike Wazowski", StringComparison.OrdinalIgnoreCase))
        {
            ApplyCoinBuffToRandomAlly(playerId, toPosition);
        }

        // Forky: Track the first move for auto-removal at the end of the first turn.
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
    /// Applies +1 move adjustment to all adjacent allayed pieces (temporary, this turn only).
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
    /// Applies +1 coin buff to a random adjacent allayed piece for their next coin collection.
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
            
            if (forky is { HasMovedOnFirstTurn: true })
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
