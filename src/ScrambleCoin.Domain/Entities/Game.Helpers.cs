using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Events;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Entities;

/// <summary>
/// Helper methods for charge movement, ice patches, and board queries.
/// Includes private utility methods for resolving complex movement scenarios.
/// </summary>
public partial class Game
{
    // ── Charge movement helpers ───────────────────────────────────────────────

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

    // ── Ice patch helpers ─────────────────────────────────────────────────────

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
    /// <param name="previousPosition">Position before landing on the ice patch (used to calculate a direction).</param>
    /// <param name="playerId">Player collecting coins during the slide.</param>
    /// <param name="pieceId">ID of the sliding piece (for error reporting).</param>
    /// <returns>The position after the slide (maybe the same as the currentPosition if blocked).</returns>
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
        if (coin is null)
            return slideTarget;
        
        
        slideTile.ClearOccupant();
        AddScore(playerId, coin.Value);
        _domainEvents.Add(new CoinCollected(
            Id, TurnNumber, playerId, pieceId, slideTarget,
            coin.CoinType, coin.Value, DateTimeOffset.UtcNow));

        return slideTarget;
    }

    /// <summary>
    /// Places ice patches on all intermediate positions that Elsa passed through.
    /// Excludes the starting position and the final destination.
    /// </summary>
    private void PlaceElsaIcePatches(List<Position> fullPath)
    {
        // Ice patches are placed in all positions except the final destination.
        // fullPath contains the visited positions in order (not including the starting position).
        for (var i = 0; i < fullPath.Count - 1; i++)
        {
            Board.PlaceIcePatch(fullPath[i]);
        }
    }

    // ── Board state helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Validates that <paramref name="playerId"/> is one of the two participants in this game.
    /// </summary>
    /// <exception cref="DomainException">
    /// Thrown when <paramref name="playerId"/> is not <see cref="PlayerOne"/> or <see cref="PlayerTwo"/>.
    /// </exception>
    private void EnsureIsParticipant(Guid playerId)
    {
        if (playerId != PlayerOne && playerId != PlayerTwo)
            throw new DomainException($"Player {playerId} is not a participant of game {Id}.");
    }
}
