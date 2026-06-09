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
        ValidateAllSegments(segments, piece, startPosition, playerId, pieceId);

        var currentPosition = startPosition;
        var fullPath = new List<Position>();

        for (var segIndex = 0; segIndex < segments.Count; segIndex++)
        {
            var segment = segments[segIndex];
            if (segment.Count == 0)
                continue;

            var segmentMovementType = piece.GetSegmentMovementType(segIndex);
            var segmentMaxDistance = piece.GetSegmentMaxDistance(segIndex);

            currentPosition = segmentMovementType switch
            {
                MovementType.Charge => HandleChargeMovement(segIndex, segment, currentPosition, pieceId, playerId, fullPath),
                MovementType.Ethereal => HandleEtherealMovement(segIndex, segment, currentPosition, pieceId, playerId, segmentMaxDistance, fullPath),
                MovementType.Jump => HandleJumpMovement(segIndex, segment, currentPosition, pieceId, playerId, piece, segmentMaxDistance, fullPath),
                MovementType.Orthogonal => HandleOrthogonalMovement(segIndex, segment, currentPosition, pieceId, playerId, piece, segmentMaxDistance, fullPath),
                MovementType.Diagonal => HandleDiagonalMovement(segIndex, segment, currentPosition, pieceId, playerId, segmentMaxDistance, fullPath),
                MovementType.AnyDirection => HandleAnyDirectionMovement(segIndex, segment, currentPosition, pieceId, playerId, segmentMaxDistance, fullPath),
                _ => throw new DomainException(
                    $"Unknown movement type: {segmentMovementType}. This should never happen.")
            };
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
            PlaceElsaIcePatches(fullPath);
        }

        // Execute on-stop abilities (Issue #49)
        ExecuteOnStopAbility(piece, playerId);

        // Execute passive abilities triggered by move (Issue #50)
        OnPieceMoved(playerId, piece, startPosition, currentPosition);

        _movedPieceIds.Add(pieceId);

        // Advance the active player when all their on-board pieces have moved.
        TryAutoAdvanceMovePhase();
    }

    private void ValidateAllSegments(
        IReadOnlyList<IReadOnlyList<Position>> segments,
        Piece piece,
        Position startPosition,
        Guid playerId,
        Guid pieceId)
    {
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

        var currentPosition = startPosition;

        for (var segIndex = 0; segIndex < segments.Count; segIndex++)
        {
            var segment = segments[segIndex];
            var segmentMovementType = piece.GetSegmentMovementType(segIndex);
            var segmentMaxDistance = piece.GetSegmentMaxDistance(segIndex);

            if (segment.Count == 0)
            {
                var hasValidMoveAtCurrentPos = segmentMovementType == MovementType.Jump
                    ? Board.HasAnyValidMove(currentPosition, segmentMovementType, segmentMaxDistance)
                    : Board.HasAnyValidMove(currentPosition, segmentMovementType);

                if (hasValidMoveAtCurrentPos)
                    throw new DomainException(
                        $"Piece {pieceId}, segment {segIndex}: an empty segment is not allowed when a valid move exists.");

                continue;
            }

            currentPosition = segmentMovementType switch
            {
                MovementType.Charge => ValidateChargeSegment(segIndex, segment, currentPosition, pieceId),
                MovementType.Ethereal => ValidateSteppedSegment(segIndex, segment, currentPosition, pieceId, segmentMaxDistance, segmentMovementType, piece),
                MovementType.Jump => ValidateJumpSegment(segIndex, segment, currentPosition, pieceId, playerId, piece, segmentMaxDistance),
                MovementType.Orthogonal => ValidateSteppedSegment(segIndex, segment, currentPosition, pieceId, segmentMaxDistance, segmentMovementType, piece),
                MovementType.Diagonal => ValidateSteppedSegment(segIndex, segment, currentPosition, pieceId, segmentMaxDistance, segmentMovementType, piece),
                MovementType.AnyDirection => ValidateSteppedSegment(segIndex, segment, currentPosition, pieceId, segmentMaxDistance, segmentMovementType, piece),
                _ => throw new DomainException(
                    $"Unknown movement type: {segmentMovementType}. This should never happen.")
            };
        }

        var finalSegmentMovementType = piece.GetSegmentMovementType(segments.Count - 1);
        if (finalSegmentMovementType != MovementType.Ethereal || currentPosition == startPosition)
            return;
        
        var destinationTile = Board.GetTile(currentPosition);
        if (destinationTile.AsPiece is not null)
            throw new DomainException(
                $"Piece {pieceId}: tile {currentPosition} is already occupied by a piece. Ethereal movement must end on a free tile.");

        if (Board.IsObstacleCovering(currentPosition))
            throw new DomainException(
                $"Piece {pieceId}: tile {currentPosition} is covered by an obstacle. Ethereal movement must end on a free tile.");
    }

    private Position ValidateChargeSegment(
        int segIndex,
        IReadOnlyList<Position> segment,
        Position currentPosition,
        Guid pieceId)
    {
        if (segment.Count != 1)
            throw new DomainException(
                $"Piece {pieceId}, segment {segIndex}: Charge movement requires exactly 1 position, but {segment.Count} were provided.");

        var chargePath = ResolveValidatedChargePath(currentPosition, segment[0], pieceId, MovementType.Charge);
        var chargeEndPosition = chargePath.Count > 0 ? chargePath[^1] : currentPosition;

        if (!Board.HasIcePatch(chargeEndPosition))
            return chargeEndPosition;
        
        var slidePreviousPosition = chargePath.Count > 1
            ? chargePath[^2]
            : currentPosition;
        chargeEndPosition = ResolveIcePatchSlideDestination(chargeEndPosition, slidePreviousPosition);

        return chargeEndPosition;
    }

    private Position ValidateSteppedSegment(
        int segIndex,
        IReadOnlyList<Position> segment,
        Position currentPosition,
        Guid pieceId,
        int segmentMaxDistance,
        MovementType segmentMovementType,
        Piece piece)
    {
        if (segment.Count > segmentMaxDistance)
            throw new DomainException(
                $"Piece {pieceId}, segment {segIndex}: segment has {segment.Count} step(s), but MaxDistance is {segmentMaxDistance}.");

        var segFrom = currentPosition;
        var isStitch = piece.Name.Equals("Stitch", StringComparison.OrdinalIgnoreCase);
        var destroyedFencePositions = isStitch ? new HashSet<Position>() : null;

        foreach (var stepTo in segment)
        {
            switch (segmentMovementType)
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
                case MovementType.Ethereal:
                    if (!segFrom.IsOrthogonallyAdjacentTo(stepTo) && !segFrom.IsDiagonallyAdjacentTo(stepTo))
                        throw new DomainException(
                            $"Piece {pieceId}: step from {segFrom} to {stepTo} is not adjacent.");
                    break;
                case MovementType.Jump:
                case MovementType.Charge:
                default:
                    throw new ArgumentOutOfRangeException(nameof(segmentMovementType), segmentMovementType, null);
            }

            if (segmentMovementType == MovementType.Ethereal)
            {
                if (IsFenceBlockedForValidation(segFrom, stepTo))
                    throw new DomainException(
                        $"Piece {pieceId}: step from {segFrom} to {stepTo} is blocked by a fence.");
            }
            else if (isStitch)
            {
                var hasFence = HasFenceForValidation(stepTo, destroyedFencePositions);
                var hasRock = Board.HasRock(stepTo);
                var hasLake = Board.HasLake(stepTo);

                if (hasRock || (hasLake && !hasFence))
                    throw new DomainException(
                        $"Piece {pieceId}: step from {segFrom} to {stepTo} is blocked by a rock or lake.");

                if (hasFence)
                    destroyedFencePositions!.Add(stepTo);
            }
            else
            {
                if (Board.IsObstacleCovering(stepTo) || IsFenceBlockedForValidation(segFrom, stepTo))
                    throw new DomainException(
                        $"Piece {pieceId}: step from {segFrom} to {stepTo} is blocked.");
            }

            if (segmentMovementType != MovementType.Ethereal && Board.GetTile(stepTo).AsPiece is not null)
                throw new DomainException(
                    $"Piece {pieceId}: step from {segFrom} to {stepTo} is occupied by a piece.");

            var positionAfterStep = stepTo;
            if (Board.HasIcePatch(positionAfterStep))
            {
                positionAfterStep = ResolveIcePatchSlideDestination(positionAfterStep, segFrom, destroyedFencePositions);
            }

            segFrom = positionAfterStep;
        }

        return segFrom;
    }

    private Position ValidateJumpSegment(
        int segIndex,
        IReadOnlyList<Position> segment,
        Position currentPosition,
        Guid pieceId,
        Guid playerId,
        Piece piece,
        int segmentMaxDistance)
    {
        if (segment.Count != 1)
            throw new DomainException(
                $"Piece {pieceId}, segment {segIndex}: Jump movement requires exactly 1 destination position, but {segment.Count} were provided.");

        var destination = segment[0];
        var rowDiff = destination.Row - currentPosition.Row;
        var colDiff = destination.Col - currentPosition.Col;

        if (rowDiff == 0 && colDiff == 0)
            throw new DomainException(
                $"Piece {pieceId}: jump destination must be different from the current position.");

        ValidateJumpDirectionConstraint(pieceId, currentPosition, destination, rowDiff, colDiff, MovementType.Jump);

        var distance = CalculateJumpDistance(currentPosition, destination);
        if (distance > segmentMaxDistance)
            throw new DomainException(
                $"Piece {pieceId}: jump from {currentPosition} to {destination} is {distance} tiles, but MaxDistance is {segmentMaxDistance}.");

        if (Board.IsObstacleCovering(destination))
            throw new DomainException(
                $"Piece {pieceId}: tile {destination} is occupied by an obstacle.");

        var targetPiece = Board.GetTile(destination).AsPiece;
        if (piece.Name.Equals("Scar", StringComparison.OrdinalIgnoreCase))
        {
            if (targetPiece is not null && targetPiece.PlayerId == playerId)
                throw new DomainException(
                    $"Piece {pieceId}: cannot land on ally piece at {destination}.");
        }
        else if (!piece.Name.Equals("Daisy", StringComparison.OrdinalIgnoreCase) && targetPiece is not null)
        {
            throw new DomainException(
                $"Piece {pieceId}: tile {destination} is already occupied by a piece.");
        }

        return destination;
    }

    private List<Position> ResolveValidatedChargePath(
        Position startPosition,
        Position firstStep,
        Guid pieceId,
        MovementType movementType)
    {
        if (!startPosition.IsOrthogonallyAdjacentTo(firstStep) && !startPosition.IsDiagonallyAdjacentTo(firstStep))
            throw new DomainException(
                $"Piece {pieceId}: first charge step from {startPosition} to {firstStep} is not adjacent.");

        var isOrthogonal = startPosition.IsOrthogonallyAdjacentTo(firstStep);
        var isDiagonal = startPosition.IsDiagonallyAdjacentTo(firstStep);

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
                    break;

                case MovementType.Jump:
                case MovementType.Charge:
                case MovementType.Ethereal:
                default:
                    //TODO: no valid move exception
                    throw new ArgumentOutOfRangeException(nameof(movementType), movementType, null);
            }
        }

        if (!Board.IsPassable(startPosition, firstStep) || 
            Board.GetTile(firstStep).AsPiece is not null)
            return [];

        var rowDelta = Math.Sign(firstStep.Row - startPosition.Row);
        var colDelta = Math.Sign(firstStep.Col - startPosition.Col);
        var chargePath = new List<Position>();
        var currentPos = firstStep;

        while (true)
        {
            chargePath.Add(currentPos);

            var nextRow = currentPos.Row + rowDelta;
            var nextCol = currentPos.Col + colDelta;
            if (nextRow < 0 || nextRow >= Board.Size || nextCol < 0 || nextCol >= Board.Size)
                break;

            var nextPos = new Position(nextRow, nextCol);
            if (!Board.IsPassable(currentPos, nextPos))
                break;

            if (Board.GetTile(nextPos).AsPiece is not null)
                break;

            currentPos = nextPos;
        }

        return chargePath;
    }

    private Position ResolveIcePatchSlideDestination(
        Position currentPosition,
        Position previousPosition,
        ISet<Position>? destroyedFencePositions = null)
    {
        var rowDelta = Math.Sign(currentPosition.Row - previousPosition.Row);
        var colDelta = Math.Sign(currentPosition.Col - previousPosition.Col);

        var slideRow = currentPosition.Row + rowDelta;
        var slideCol = currentPosition.Col + colDelta;
        if (slideRow < 0 || slideRow >= Board.Size || slideCol < 0 || slideCol >= Board.Size)
            return currentPosition;

        var slideTarget = new Position(slideRow, slideCol);
        if (Board.IsObstacleCovering(slideTarget) 
            || IsFenceBlockedForValidation(currentPosition, slideTarget, destroyedFencePositions) 
            || Board.GetTile(slideTarget).AsPiece is not null)
            return currentPosition;

        return slideTarget;
    }

    private bool HasFenceForValidation(Position position, HashSet<Position>? destroyedFencePositions = null)
    {
        if (destroyedFencePositions is null || destroyedFencePositions.Count == 0)
            return Board.HasFence(position);

        if (destroyedFencePositions.Contains(position))
            return false;

        var fences = Board.GetAllObstacles().Fences;
        var adjacentPositions = new List<Position>();
        var row = position.Row;
        var col = position.Col;

        if (row > 0) adjacentPositions.Add(new Position(row - 1, col));
        if (row < Board.Size - 1) adjacentPositions.Add(new Position(row + 1, col));
        if (col > 0) adjacentPositions.Add(new Position(row, col - 1));
        if (col < Board.Size - 1) adjacentPositions.Add(new Position(row, col + 1));

        return adjacentPositions.Any(adj =>
            !destroyedFencePositions.Contains(adj) &&
            fences.Any(f => f.IsOnEdge(position, adj)));
    }

    private bool IsFenceBlockedForValidation(Position from, Position to, ISet<Position>? destroyedFencePositions = null)
    {
        if (destroyedFencePositions is null || destroyedFencePositions.Count == 0)
            return Board.IsFenceBlocked(from, to);

        if (!from.IsOrthogonallyAdjacentTo(to) && !from.IsDiagonallyAdjacentTo(to))
            throw new DomainException($"Positions {from} and {to} are not adjacent.");

        var fences = Board.GetAllObstacles().Fences;

        var rowDiff = to.Row - from.Row;
        var colDiff = to.Col - from.Col;

        var isOrthogonal = Math.Abs(rowDiff) == 1 && colDiff == 0 ||
                           rowDiff == 0 && Math.Abs(colDiff) == 1;

        var isDiagonal = Math.Abs(rowDiff) == 1 && Math.Abs(colDiff) == 1;

        if (isOrthogonal)
            return HasActiveFenceOnEdge(from, to);

        if (!isDiagonal)
            return false;

        var cornerA = new Position(from.Row + rowDiff, from.Col);
        var cornerB = new Position(from.Row, from.Col + colDiff);

        var fenceFromVertical = HasActiveFenceOnEdge(from, cornerA);
        var fenceFromHorizontal = HasActiveFenceOnEdge(from, cornerB);
        if (fenceFromVertical && fenceFromHorizontal)
            return true;

        var fenceToVertical = HasActiveFenceOnEdge(to, cornerA);
        var fenceToHorizontal = HasActiveFenceOnEdge(to, cornerB);
        return fenceToVertical && fenceToHorizontal;

        bool HasActiveFenceOnEdge(Position edgeStart, Position edgeEnd) =>
            !destroyedFencePositions.Contains(edgeStart) &&
            !destroyedFencePositions.Contains(edgeEnd) &&
            fences.Any(f => f.IsOnEdge(edgeStart, edgeEnd));
    }

    private Position HandleChargeMovement(
        int segIndex,
        IReadOnlyList<Position> segment,
        Position currentPosition,
        Guid pieceId,
        Guid playerId,
        List<Position> fullPath)
    {
        if (segment.Count != 1)
            throw new DomainException(
                $"Piece {pieceId}, segment {segIndex}: Charge movement requires exactly 1 position, but {segment.Count} were provided.");

        var firstStep = segment[0];
        var chargePath = ResolveChargePath(currentPosition, firstStep, pieceId, MovementType.Charge, playerId);

        fullPath.AddRange(chargePath);
        var chargeEndPosition = chargePath.Count > 0 ? chargePath[^1] : currentPosition;

        if (!Board.HasIcePatch(chargeEndPosition))
            return chargeEndPosition;
        
        var slidePreviousPosition = chargePath.Count > 1
            ? chargePath[^2]
            : currentPosition;
        var slidePosition = ApplyIcePatchSlide(chargeEndPosition, slidePreviousPosition, playerId, pieceId);
        if (slidePosition == chargeEndPosition)
            return chargeEndPosition;
        
        fullPath.Add(slidePosition);
        chargeEndPosition = slidePosition;

        return chargeEndPosition;
    }

    private Position HandleEtherealMovement(
        int segIndex,
        IReadOnlyList<Position> segment,
        Position currentPosition,
        Guid pieceId,
        Guid playerId,
        int segmentMaxDistance,
        List<Position> fullPath)
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

            if (Board.IsFenceBlocked(segFrom, stepTo))
                throw new DomainException(
                    $"Piece {pieceId}: step from {segFrom} to {stepTo} is blocked by a fence.");

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

            if (Board.HasIcePatch(positionAfterStep))
            {
                var slidePosition = ApplyIcePatchSlide(positionAfterStep, segFrom, playerId, pieceId);
                if (slidePosition != positionAfterStep)
                {
                    fullPath.Add(slidePosition);
                    positionAfterStep = slidePosition;
                }
            }

            segFrom = positionAfterStep;
        }

        return segFrom;
    }

    private Position HandleJumpMovement(
        int segIndex,
        IReadOnlyList<Position> segment,
        Position currentPosition,
        Guid pieceId,
        Guid playerId,
        Piece piece,
        int segmentMaxDistance,
        List<Position> fullPath)
    {
        if (segment.Count != 1)
            throw new DomainException(
                $"Piece {pieceId}, segment {segIndex}: Jump movement requires exactly 1 destination position, but {segment.Count} were provided.");

        var destination = segment[0];
        var rowDiff = destination.Row - currentPosition.Row;
        var colDiff = destination.Col - currentPosition.Col;

        if (rowDiff == 0 && colDiff == 0)
            throw new DomainException(
                $"Piece {pieceId}: jump destination must be different from the current position.");

        ValidateJumpDirectionConstraint(pieceId, currentPosition, destination, rowDiff, colDiff, MovementType.Jump);

        var distance = CalculateJumpDistance(currentPosition, destination);

        if (distance > segmentMaxDistance)
            throw new DomainException(
                $"Piece {pieceId}: jump from {currentPosition} to {destination} is {distance} tiles, but MaxDistance is {segmentMaxDistance}.");

        if (Board.IsObstacleCovering(destination))
            throw new DomainException(
                $"Piece {pieceId}: tile {destination} is occupied by an obstacle.");

        var destinationTile = Board.GetTile(destination);
        var targetPiece = destinationTile.AsPiece;

        if (piece.Name.Equals("Scar", StringComparison.OrdinalIgnoreCase))
        {
            if (targetPiece is not null && targetPiece.PlayerId != playerId)
            {
                destinationTile.ClearOccupant();
                targetPiece.RemoveFromBoard();
                _domainEvents.Add(new PieceRemoved(
                    Id, TurnNumber, targetPiece.Id, pieceId,
                    destination, DateTimeOffset.UtcNow));
            }
            else if (targetPiece is not null)
            {
                throw new DomainException(
                    $"Piece {pieceId}: cannot land on ally piece at {destination}.");
            }
        }
        else if (piece.Name.Equals("Daisy", StringComparison.OrdinalIgnoreCase))
        {
            if (targetPiece is not null)
            {
                var daisyTile = Board.GetTile(currentPosition);
                daisyTile.ClearOccupant();
                destinationTile.ClearOccupant();

                daisyTile.SetOccupant(targetPiece);
                targetPiece.PlaceAt(currentPosition);

                fullPath.Add(currentPosition);

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
            }
        }
        else if (targetPiece is not null)
        {
            throw new DomainException(
                $"Piece {pieceId}: tile {destination} is already occupied by a piece.");
        }

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
        return destination;
    }

    private Position HandleOrthogonalMovement(
        int segIndex,
        IReadOnlyList<Position> segment,
        Position currentPosition,
        Guid pieceId,
        Guid playerId,
        Piece piece,
        int segmentMaxDistance,
        List<Position> fullPath)
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

            var isStitch = piece.Name.Equals("Stitch", StringComparison.OrdinalIgnoreCase);
            if (isStitch)
            {
                var hasRock = Board.HasRock(stepTo);
                var hasLake = Board.IsObstacleCovering(stepTo);

                if (hasRock || (hasLake && !Board.HasFence(stepTo)))
                    throw new DomainException(
                        $"Piece {pieceId}: step from {segFrom} to {stepTo} is blocked by a rock or lake.");

                if (Board.HasFence(stepTo))
                {
                    Board.DestroyFence(stepTo);
                    _domainEvents.Add(new FenceDestroyed(
                        Id, TurnNumber, stepTo, DateTimeOffset.UtcNow));
                }
            }
            else
            {
                if (Board.IsObstacleCovering(stepTo) || Board.IsFenceBlocked(segFrom, stepTo))
                    throw new DomainException(
                        $"Piece {pieceId}: step from {segFrom} to {stepTo} is blocked.");
            }

            var stepTile = Board.GetTile(stepTo);
            if (stepTile.AsPiece is not null)
                throw new DomainException(
                    $"Piece {pieceId}: step from {segFrom} to {stepTo} is occupied by a piece.");

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

            if (Board.HasIcePatch(positionAfterStep))
            {
                var slidePosition = ApplyIcePatchSlide(positionAfterStep, segFrom, playerId, pieceId);
                if (slidePosition != positionAfterStep)
                {
                    fullPath.Add(slidePosition);
                    positionAfterStep = slidePosition;
                }
            }

            segFrom = positionAfterStep;
        }

        return segFrom;
    }

    private Position HandleDiagonalMovement(
        int segIndex,
        IReadOnlyList<Position> segment,
        Position currentPosition,
        Guid pieceId,
        Guid playerId,
        int segmentMaxDistance,
        List<Position> fullPath)
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

            if (Board.IsObstacleCovering(stepTo) || Board.IsFenceBlocked(segFrom, stepTo))
                throw new DomainException(
                    $"Piece {pieceId}: step from {segFrom} to {stepTo} is blocked.");

            var stepTile = Board.GetTile(stepTo);
            if (stepTile.AsPiece is not null)
                throw new DomainException(
                    $"Piece {pieceId}: step from {segFrom} to {stepTo} is occupied by a piece.");

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

            if (Board.HasIcePatch(positionAfterStep))
            {
                var slidePosition = ApplyIcePatchSlide(positionAfterStep, segFrom, playerId, pieceId);
                if (slidePosition != positionAfterStep)
                {
                    fullPath.Add(slidePosition);
                    positionAfterStep = slidePosition;
                }
            }

            segFrom = positionAfterStep;
        }

        return segFrom;
    }

    private Position HandleAnyDirectionMovement(
        int segIndex,
        IReadOnlyList<Position> segment,
        Position currentPosition,
        Guid pieceId,
        Guid playerId,
        int segmentMaxDistance,
        List<Position> fullPath)
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

            if (Board.IsObstacleCovering(stepTo) || Board.IsFenceBlocked(segFrom, stepTo))
                throw new DomainException(
                    $"Piece {pieceId}: step from {segFrom} to {stepTo} is blocked.");

            var stepTile = Board.GetTile(stepTo);
            if (stepTile.AsPiece is not null)
                throw new DomainException(
                    $"Piece {pieceId}: step from {segFrom} to {stepTo} is occupied by a piece.");

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

            if (Board.HasIcePatch(positionAfterStep))
            {
                var slidePosition = ApplyIcePatchSlide(positionAfterStep, segFrom, playerId, pieceId);
                if (slidePosition != positionAfterStep)
                {
                    fullPath.Add(slidePosition);
                    positionAfterStep = slidePosition;
                }
            }

            segFrom = positionAfterStep;
        }

        return segFrom;
    }

    /// <summary>
    /// Validates that a jump destination respects the piece's directional constraint.
    /// Jump pieces can have directional modifiers (Orthogonal-Jump, Diagonal-Jump, AnyDirection-Jump).
    /// </summary>
    private static void ValidateJumpDirectionConstraint(
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
            case MovementType.Charge:
            case MovementType.Ethereal:
            default:
                throw new ArgumentOutOfRangeException(nameof(movementType), movementType, null);
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
    private static int CalculateJumpDistance(Position from, Position to)
    {
        // All Jump movement types use Chebyshev distance (max of |Δrow|, |Δcol|)
        return to.ChebyshevDistance(from);
    }
}
