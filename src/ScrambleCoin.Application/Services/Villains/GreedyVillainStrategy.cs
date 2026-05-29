using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Services.Villains;

/// <summary>
/// Base class for greedy villain strategies.
/// Villains use a simple greedy algorithm:
/// - PlacePhase: place the next unplaced piece on the nearest free border tile (for Borders-entry pieces)
/// - MovePhase: move each on-board piece toward the nearest coin
/// </summary>
public abstract class GreedyVillainStrategy : IVillainStrategy
{
    public VillainAction DecideAction(Game game, Guid villainPlayerId)
    {
        return game.CurrentPhase switch
        {
            TurnPhase.PlacePhase => DecidePlacement(game, villainPlayerId),
            TurnPhase.MovePhase => DecideMovement(game, villainPlayerId),
            _ => throw new DomainException($"Invalid phase for villain action: {game.CurrentPhase}")
        };
    }

    /// <summary>
    /// Decides placement action: place the next unplaced piece on the nearest free border tile,
    /// or skip if at max pieces or no valid placement.
    /// </summary>
    private static VillainAction DecidePlacement(Game game, Guid villainPlayerId)
    {
        var lineup = game.CurrentPhase == TurnPhase.PlacePhase
            ? (villainPlayerId == game.PlayerOne ? game.LineupPlayerOne : game.LineupPlayerTwo)
            : throw new DomainException("Cannot get lineup outside of active game");

        if (lineup is null)
            return new SkipPlacementAction();

        // Check if the villain has reached the 3-piece limit
        if (game.PiecesOnBoard[villainPlayerId] >= Game.MaxPiecesOnBoard)
            return new SkipPlacementAction();

        // Find the next unplaced piece
        var nextPiece = lineup.Pieces.FirstOrDefault(p => !p.IsOnBoard);
        if (nextPiece is null)
            return new SkipPlacementAction();

        // Find the nearest free border tile for placing the piece
        var position = FindNearestFreeBorderTile(game, nextPiece);
        if (position is null)
            return new SkipPlacementAction();

        return new PlacementAction(nextPiece.Id, position);
    }

    /// <summary>
    /// Decides movement action: for each on-board piece, generate a move segment toward the nearest coin.
    /// If no valid moves, skip.
    /// </summary>
    private static VillainAction DecideMovement(Game game, Guid villainPlayerId)
    {
        var lineup = villainPlayerId == game.PlayerOne ? game.LineupPlayerOne : game.LineupPlayerTwo;
        if (lineup is null)
            return new SkipMovementAction();

        var villainPieces = lineup.Pieces
            .Where(p => p.IsOnBoard)
            .ToList();

        if (villainPieces.Count == 0)
            return new SkipMovementAction();

        // Try to move the first piece toward the nearest coin
        var piece = villainPieces.First();
        var pathTowardsCoin = FindPathTowardNearestCoin(game, piece);

        if (pathTowardsCoin is null || pathTowardsCoin.Count == 0)
            return new SkipMovementAction();

        // For simplicity, move only the first piece with one segment.
        // This is a greedy approach: move the first piece toward the nearest coin.
        var segments = new List<IReadOnlyList<Position>> { pathTowardsCoin };
        return new MovementAction(piece.Id, segments);
    }

    /// <summary>
    /// Finds the nearest free border tile to place a piece.
    /// Uses Manhattan distance for simplicity.
    /// </summary>
    private static Position? FindNearestFreeBorderTile(Game game, Piece piece)
    {
        var freeBorderTiles = new List<Position>();

        // Collect all free border tiles
        for (var row = 0; row < Board.Size; row++)
        {
            for (var col = 0; col < Board.Size; col++)
            {
                var position = new Position(row, col);

                // Check if the position is a valid border entry point
                if (!Board.IsValidEntryPoint(position, piece.EntryPointType))
                    continue;

                // Check if the tile is free (no piece and no obstacle)
                if (!game.Board.IsEmpty(position))
                    continue;

                if (game.Board.IsObstacleCovering(position))
                    continue;

                freeBorderTiles.Add(position);
            }
        }

        if (freeBorderTiles.Count == 0)
            return null;

        // Return the nearest free border tile (using Manhattan distance)
        // For now, use the centre of the board (3, 3) as a reference point
        const int referenceRow = 3;
        const int referenceCol = 3;

        return freeBorderTiles
            .OrderBy(p => Math.Abs(p.Row - referenceRow) + Math.Abs(p.Col - referenceCol))
            .FirstOrDefault();
    }

    /// <summary>
    /// Finds a path from the piece toward the nearest coin.
    /// Returns a list of positions representing the path (not including the current position).
    /// Uses Manhattan distance to find the nearest coin.
    /// </summary>
    private static List<Position>? FindPathTowardNearestCoin(Game game, Piece piece)
    {
        if (piece.Position is null)
            return null;

        // Get all coins on the board
        var coins = game.Board.GetAllCoins();
        if (coins.Count == 0)
            return [];

        // Find the nearest coin
        var nearestCoinPos = coins
            .OrderBy(tile => ManhattanDistance(piece.Position, tile.Position))
            .FirstOrDefault()?.Position;

        if (nearestCoinPos is null)
            return [];

        // For simplicity: move one step toward the coin
        // This is a greedy approach: move in the direction that reduces distance most
        var nextPos = MoveTowardTarget(piece.Position, nearestCoinPos, game, piece);

        return nextPos is null ?
            [] :
            [
                nextPos
            ];
    }

    /// <summary>
    /// Moves one step from the current position toward the target position.
    /// For orthogonal pieces, prefer orthogonal moves; for diagonal, prefer diagonal moves; etc.
    /// </summary>
    private static Position? MoveTowardTarget(Position current, Position target, Game game, Piece piece)
    {
        var candidates = new List<Position>();

        // Generate adjacent candidates based on a movement type
        var adjacents = GetAdjacentPositions(current, piece.MovementType);

        foreach (var adjacent in adjacents)
        {
            // Check if the position is within bounds
            if (!Board.IsWithinBounds(adjacent))
                continue;

            // Check if the position is free (not occupied by a piece or obstacle)
            if (!game.Board.IsEmpty(adjacent))
                continue;

            if (game.Board.IsObstacleCovering(adjacent))
                continue;

            candidates.Add(adjacent);
        }

        if (candidates.Count == 0)
            return null;

        // Return the candidate that reduces distance to target the most
        return candidates
            .OrderBy(p => ManhattanDistance(p, target))
            .FirstOrDefault();
    }

    /// <summary>
    /// Gets adjacent positions based on a movement type.
    /// </summary>
    private static List<Position> GetAdjacentPositions(Position from, MovementType movementType)
    {
        var candidates = new List<Position>();

        if (movementType is MovementType.Orthogonal or MovementType.AnyDirection)
        {
            // Orthogonal moves
            candidates.Add(new Position(from.Row - 1, from.Col)); // Up
            candidates.Add(new Position(from.Row + 1, from.Col)); // Down
            candidates.Add(new Position(from.Row, from.Col - 1)); // Left
            candidates.Add(new Position(from.Row, from.Col + 1)); // Right
        }

        if (movementType is not (MovementType.Diagonal or MovementType.AnyDirection))
        {
            return candidates
                .Where(Board.IsWithinBounds)
                .ToList();
        }
        
        // Diagonal moves
        candidates.Add(new Position(from.Row - 1, from.Col - 1)); // Up-Left
        candidates.Add(new Position(from.Row - 1, from.Col + 1)); // Up-Right
        candidates.Add(new Position(from.Row + 1, from.Col - 1)); // Down-Left
        candidates.Add(new Position(from.Row + 1, from.Col + 1)); // Down-Right

        return candidates
            .Where(Board.IsWithinBounds)
            .ToList();
    }

    /// <summary>
    /// Calculates Manhattan distance between two positions.
    /// </summary>
    private static int ManhattanDistance(Position from, Position to) =>
        Math.Abs(from.Row - to.Row) + Math.Abs(from.Col - to.Col);
}
