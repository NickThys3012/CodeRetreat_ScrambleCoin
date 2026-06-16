using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Services.Villains;

/// <summary>
/// Base class for greedy villain strategies. Villains use a simple, deterministic greedy algorithm:
/// <list type="bullet">
///   <item><description>PlacePhase: place the next unplaced (and currently available) piece on the
///   free, legal entry tile nearest to the closest coin — or skip when capped/blocked.</description></item>
///   <item><description>MovePhase: move the next unmoved on-board piece one legal step toward the
///   nearest coin — or skip movement when no on-board piece can move.</description></item>
/// </list>
/// All candidate moves are validated against the same <see cref="Board"/> passability and
/// movement rules the domain enforces, so every produced action is legal for the domain to apply.
/// </summary>
public abstract class GreedyVillainStrategy : IVillainStrategy
{
    /// <inheritdoc/>
    public VillainAction DecideAction(Game game, Guid villainPlayerId) =>
        game.CurrentPhase switch
        {
            TurnPhase.PlacePhase => DecidePlacement(game, villainPlayerId),
            TurnPhase.MovePhase => DecideMovement(game, villainPlayerId),
            _ => throw new DomainException(
                $"Villain cannot act during phase '{game.CurrentPhase?.ToString() ?? "None"}'.")
        };

    // ── Placement ─────────────────────────────────────────────────────────────

    private static VillainAction DecidePlacement(Game game, Guid villainPlayerId)
    {
        var lineup = GetLineup(game, villainPlayerId);
        if (lineup is null)
            return new SkipPlacementAction();

        // Respect the 3-piece-on-board cap.
        if (game.GetPiecesOnBoardCount(villainPlayerId) >= Game.MaxPiecesOnBoard)
            return new SkipPlacementAction();

        // Pick the first lineup piece that is off-board and currently available this turn.
        var nextPiece = lineup.Pieces.FirstOrDefault(p =>
            !p.IsOnBoard &&
            (p.AvailableFromTurn is null || game.CurrentTurnNumber >= p.AvailableFromTurn));

        if (nextPiece is null)
            return new SkipPlacementAction();

        var position = FindBestEntryTile(game.Board, nextPiece);
        return position is null
            ? new SkipPlacementAction()
            : new PlacementAction(nextPiece.Id, position);
    }

    /// <summary>
    /// Finds the free, legal entry tile for <paramref name="piece"/> that is closest to the
    /// nearest coin (or, when no coins exist, closest to the board centre).
    /// </summary>
    private static Position? FindBestEntryTile(Board board, Piece piece)
    {
        var freeEntryTiles = new List<Position>();
        for (var row = 0; row < Board.Size; row++)
        for (var col = 0; col < Board.Size; col++)
        {
            var position = new Position(row, col);
            if (!Board.IsValidEntryPoint(position, piece.EntryPointType))
                continue;
            if (!board.IsEmpty(position) || board.IsObstacleCovering(position))
                continue;
            freeEntryTiles.Add(position);
        }

        if (freeEntryTiles.Count == 0)
            return null;

        var reference = NearestCoinPosition(board, freeEntryTiles[0])
                        ?? new Position(Board.Size / 2, Board.Size / 2);

        return freeEntryTiles
            .OrderBy(p => ManhattanDistance(p, reference))
            .ThenBy(p => p.Row)
            .ThenBy(p => p.Col)
            .First();
    }

    // ── Movement ──────────────────────────────────────────────────────────────

    private static VillainAction DecideMovement(Game game, Guid villainPlayerId)
    {
        var lineup = GetLineup(game, villainPlayerId);
        if (lineup is null)
            return new SkipMovementAction();

        // Only consider on-board pieces that have not already moved this turn.
        var piece = lineup.Pieces
            .FirstOrDefault(p => p.IsOnBoard && !game.HasPieceMovedThisTurn(p.Id));

        if (piece is null)
            return new SkipMovementAction();

        var target = NearestCoinPosition(game.Board, piece.Position!);
        if (target is null)
            return new SkipMovementAction();

        var segments = BuildSegmentsToward(game.Board, piece, target);
        return segments is null
            ? new SkipMovementAction()
            : new MovementAction(piece.Id, segments);
    }

    /// <summary>
    /// Builds one segment per <see cref="Piece.MovesPerTurn"/>, each a single legal step toward
    /// <paramref name="target"/>. A segment is left empty only when the piece has no legal move at
    /// that point (mirroring the domain's "stuck" allowance). Returns <c>null</c> when the piece
    /// cannot make any move at all (so the caller can skip movement instead).
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<Position>>? BuildSegmentsToward(
        Board board, Piece piece, Position target)
    {
        var segments = new List<IReadOnlyList<Position>>();
        var current = piece.Position!;
        var producedAnyMove = false;

        for (var segIndex = 0; segIndex < piece.MovesPerTurn; segIndex++)
        {
            var segType = piece.GetSegmentMovementType(segIndex);
            var segMax = piece.GetSegmentMaxDistance(segIndex);

            var step = BuildStep(board, current, segType, segMax, target);
            if (step is null)
            {
                // No legal move from here for this segment type → leave it empty (allowed by domain).
                segments.Add(Array.Empty<Position>());
                continue;
            }

            segments.Add(step);
            current = step[^1];
            producedAnyMove = true;
        }

        return producedAnyMove ? segments : null;
    }

    /// <summary>
    /// Builds a single legal step from <paramref name="from"/> toward <paramref name="target"/> for
    /// the given <paramref name="movementType"/>, or <c>null</c> when no legal move exists.
    /// </summary>
    private static IReadOnlyList<Position>? BuildStep(
        Board board, Position from, MovementType movementType, int maxDistance, Position target) =>
        movementType switch
        {
            MovementType.Jump => BuildJumpStep(board, from, maxDistance, target),
            MovementType.Charge => BuildChargeStep(board, from, target),
            _ => BuildSteppedStep(board, from, movementType, target)
        };

    /// <summary>
    /// One-tile step for Orthogonal/Diagonal/AnyDirection/Ethereal movement types.
    /// </summary>
    private static IReadOnlyList<Position>? BuildSteppedStep(
        Board board, Position from, MovementType movementType, Position target)
    {
        var best = AdjacentDeltas(movementType)
            .Select(d => (row: from.Row + d.dr, col: from.Col + d.dc))
            .Where(c => c.row is >= 0 and < Board.Size && c.col is >= 0 and < Board.Size)
            .Select(c => new Position(c.row, c.col))
            .Where(to => IsSteppedStepLegal(board, from, to, movementType))
            .OrderBy(to => ManhattanDistance(to, target))
            .ThenBy(to => to.Row)
            .ThenBy(to => to.Col)
            .FirstOrDefault();

        return best is null ? null : new[] { best };
    }

    private static bool IsSteppedStepLegal(Board board, Position from, Position to, MovementType movementType)
    {
        if (movementType == MovementType.Ethereal)
        {
            // Ethereal ignores obstacles en route but must end on a free tile and respect fences.
            if (board.IsFenceBlocked(from, to))
                return false;
            return !board.IsObstacleCovering(to) && board.GetTile(to).AsPiece is null;
        }

        // Passability covers rocks, lakes, and fences; the destination must also be unoccupied.
        return board.IsPassable(from, to) && board.GetTile(to).AsPiece is null;
    }

    /// <summary>
    /// Jump step: choose an unoccupied destination within range that minimises distance to the target.
    /// </summary>
    private static IReadOnlyList<Position>? BuildJumpStep(
        Board board, Position from, int maxDistance, Position target)
    {
        if (maxDistance <= 0)
            return null;

        Position? best = null;
        var bestDistance = int.MaxValue;

        for (var row = 0; row < Board.Size; row++)
        for (var col = 0; col < Board.Size; col++)
        {
            var to = new Position(row, col);
            if (to.Equals(from))
                continue;
            if (from.ChebyshevDistance(to) > maxDistance)
                continue;
            if (board.IsObstacleCovering(to) || board.GetTile(to).AsPiece is not null)
                continue;

            var distance = ManhattanDistance(to, target);
            if (distance >= bestDistance)
                continue;
            bestDistance = distance;
            best = to;
        }

        return best is null ? null : new[] { best };
    }

    /// <summary>
    /// Charge step: choose an adjacent first-step direction (the piece then slides automatically)
    /// that reduces distance to the target. The segment is the single first-step position.
    /// </summary>
    private static IReadOnlyList<Position>? BuildChargeStep(Board board, Position from, Position target)
    {
        var best = AdjacentDeltas(MovementType.AnyDirection)
            .Select(d => (row: from.Row + d.dr, col: from.Col + d.dc))
            .Where(c => c.row is >= 0 and < Board.Size && c.col is >= 0 and < Board.Size)
            .Select(c => new Position(c.row, c.col))
            .Where(to => board.IsPassable(from, to) && board.GetTile(to).AsPiece is null)
            .OrderBy(to => ManhattanDistance(to, target))
            .ThenBy(to => to.Row)
            .ThenBy(to => to.Col)
            .FirstOrDefault();

        return best is null ? null : new[] { best };
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    private static Lineup? GetLineup(Game game, Guid villainPlayerId) =>
        villainPlayerId == game.PlayerOne ? game.LineupPlayerOne : game.LineupPlayerTwo;

    private static Position? NearestCoinPosition(Board board, Position from)
    {
        var coins = board.GetAllCoins();
        if (coins.Count == 0)
            return null;

        return coins
            .Select(tile => tile.Position)
            .OrderBy(pos => ManhattanDistance(from, pos))
            .ThenBy(pos => pos.Row)
            .ThenBy(pos => pos.Col)
            .First();
    }

    private static IReadOnlyList<(int dr, int dc)> AdjacentDeltas(MovementType movementType) =>
        movementType switch
        {
            MovementType.Orthogonal => new[] { (-1, 0), (1, 0), (0, -1), (0, 1) },
            MovementType.Diagonal => new[] { (-1, -1), (-1, 1), (1, -1), (1, 1) },
            _ => new[] { (-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (-1, 1), (1, -1), (1, 1) }
        };

    private static int ManhattanDistance(Position from, Position to) =>
        Math.Abs(from.Row - to.Row) + Math.Abs(from.Col - to.Col);
}
