using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Events;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Entities;

/// <summary>
/// Piece movement and movement validation logic.
/// Handles MovePiece orchestration and segment-level movement type validation.
/// </summary>
public partial class Game
{
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

                            // Check if a tile is occupied by an opponent or allay piece (cannot land on pieces)
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

                            // Check if a piece occupies a tile (cannot land on pieces)
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

                            // Check if a piece occupies a tile (cannot land on pieces)
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
        // Note: For multistep pieces, only check the last segment's movement type
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
}
