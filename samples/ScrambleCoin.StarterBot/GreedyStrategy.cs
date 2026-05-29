using ScrambleCoin.StarterBot.Models;

namespace ScrambleCoin.StarterBot;

/// <summary>
/// Default greedy strategy:
/// <list type="bullet">
///   <item><b>PlacePhase</b>: place the piece on the first free border tile.</item>
///   <item><b>MovePhase</b>: move each piece one step toward the nearest coin.
///   If no coins remain, the piece stays still.</item>
/// </list>
/// Replace this class (or implement <see cref="IStrategy"/>) to customise the bot.
/// </summary>
public sealed class GreedyStrategy : IStrategy
{
    private const int BoardSize = 8;

    // ── PlacePhase ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public PlacementDecision DecidePlacement(BoardState state, PieceState piece)
    {
        // Collect all occupied positions (obstacles, pieces, coins)
        var occupied = GetOccupiedPositions(state);

        // Choose candidate tiles based on the piece's entry-point type
        var candidates = GetCandidateTiles(state, piece);

        foreach (var pos in candidates)
        {
            if (!occupied.Contains((pos.Row, pos.Col)))
            {
                Console.WriteLine($"  → Placing {piece.Name} at {pos}");
                return new PlacementDecision.Place(piece.PieceId, pos);
            }
        }

        // No free tile found — skip this piece for now
        Console.WriteLine($"  → No free tile for {piece.Name}, skipping");
        return new PlacementDecision.Skip();
    }

    // ── MovePhase ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public MoveDecision DecideMove(BoardState state, PieceState piece)
    {
        var segments = new List<IReadOnlyList<Position>>();

        // Build a set of positions blocked by obstacles or other pieces
        var blocked = GetBlockedPositions(state);

        var currentPos = piece.Position!; // Position is non-null when IsOnBoard

        for (var seg = 0; seg < piece.MovesPerTurn; seg++)
        {
            var step = ChooseStep(state, currentPos, piece, blocked);

            if (step is not null)
            {
                segments.Add(new List<Position> { step }.AsReadOnly());
                currentPos = step; // Update position for next segment
                blocked.Add((step.Row, step.Col)); // Treat destination as blocked for subsequent segments
            }
            else
            {
                // Stay still for this segment
                segments.Add(Array.Empty<Position>());
            }
        }

        return new MoveDecision(piece.PieceId, segments.AsReadOnly());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Position? ChooseStep(BoardState state, Position from, PieceState piece, HashSet<(int, int)> blocked)
    {
        if (state.AvailableCoins.Count == 0)
            return null; // No coins — stay still

        // Find nearest coin (by Euclidean distance)
        var target = state.AvailableCoins
            .OrderBy(c => from.DistanceTo(c.Position))
            .First().Position;

        Console.WriteLine($"    {piece.Name} at {from} → nearest coin at {target}");

        // Generate candidate steps based on movement type
        var candidates = GetMoveCandidates(from, piece.MovementType);

        // Filter: stay on board, not blocked
        var valid = candidates
            .Where(p => p.Row is >= 0 and < BoardSize && p.Col is >= 0 and < BoardSize)
            .Where(p => !blocked.Contains((p.Row, p.Col)))
            .ToList();

        if (valid.Count == 0)
            return null;

        // Pick the step closest to the target coin
        return valid.OrderBy(p => p.DistanceTo(target)).First();
    }

    /// <summary>Returns all adjacent positions the piece can step to in one action.</summary>
    private static IEnumerable<Position> GetMoveCandidates(Position from, string movementType)
    {
        return movementType switch
        {
            "Orthogonal" or "Charge" => OrthogonalNeighbours(from),
            "Diagonal"               => DiagonalNeighbours(from),
            "AnyDirection"           => AllNeighbours(from),
            "Jump"                   => AllNeighbours(from), // Simplified: treat jump as 1-step any-direction
            "Ethereal"               => AllNeighbours(from), // Simplified: treat ethereal as 1-step any-direction
            _                        => AllNeighbours(from)
        };
    }

    private static IEnumerable<Position> OrthogonalNeighbours(Position p) =>
    [
        new(p.Row - 1, p.Col),
        new(p.Row + 1, p.Col),
        new(p.Row, p.Col - 1),
        new(p.Row, p.Col + 1)
    ];

    private static IEnumerable<Position> DiagonalNeighbours(Position p) =>
    [
        new(p.Row - 1, p.Col - 1),
        new(p.Row - 1, p.Col + 1),
        new(p.Row + 1, p.Col - 1),
        new(p.Row + 1, p.Col + 1)
    ];

    private static IEnumerable<Position> AllNeighbours(Position p) =>
        OrthogonalNeighbours(p).Concat(DiagonalNeighbours(p));

    /// <summary>
    /// Returns tiles the piece may be placed on, ordered front-to-back
    /// (corners first for corner-entry pieces, border sweep for border pieces).
    /// </summary>
    private static IEnumerable<Position> GetCandidateTiles(BoardState state, PieceState piece)
    {
        // Determine entry point type from board metadata (not exposed in BoardState),
        // so we use a heuristic based on piece name/movement.
        // Starter lineup pieces all use Borders or Corners.
        // For simplicity, try corners first, then full border sweep.
        var corners = new[]
        {
            new Position(0, 0), new Position(0, BoardSize - 1),
            new Position(BoardSize - 1, 0), new Position(BoardSize - 1, BoardSize - 1)
        };
        var borders = BorderTiles().ToArray();

        return corners.Concat(borders.Except(corners, PositionEqualityComparer.Instance));
    }

    private static IEnumerable<Position> BorderTiles()
    {
        for (var col = 0; col < BoardSize; col++) yield return new Position(0, col);           // top row
        for (var col = 0; col < BoardSize; col++) yield return new Position(BoardSize - 1, col); // bottom row
        for (var row = 1; row < BoardSize - 1; row++) yield return new Position(row, 0);       // left col
        for (var row = 1; row < BoardSize - 1; row++) yield return new Position(row, BoardSize - 1); // right col
    }

    /// <summary>Positions blocked by obstacles or any piece on the board.</summary>
    private static HashSet<(int, int)> GetBlockedPositions(BoardState state)
    {
        var blocked = new HashSet<(int, int)>();

        foreach (var tile in state.Board.Tiles)
        {
            if (tile.IsObstacle)
                blocked.Add((tile.Position.Row, tile.Position.Col));
            if (tile.Occupant?.Type == "piece")
                blocked.Add((tile.Position.Row, tile.Position.Col));
        }

        return blocked;
    }

    /// <summary>All positions currently occupied (obstacles, pieces, coins).</summary>
    private static HashSet<(int, int)> GetOccupiedPositions(BoardState state)
    {
        var occupied = GetBlockedPositions(state);
        foreach (var coin in state.AvailableCoins)
            occupied.Add((coin.Position.Row, coin.Position.Col));
        return occupied;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private sealed class PositionEqualityComparer : IEqualityComparer<Position>
    {
        public static readonly PositionEqualityComparer Instance = new();
        public bool Equals(Position? x, Position? y) => x?.Row == y?.Row && x?.Col == y?.Col;
        public int GetHashCode(Position p) => HashCode.Combine(p.Row, p.Col);
    }
}
