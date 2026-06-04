using ScrambleCoin.StarterBot.Models;

namespace ScrambleCoin.StarterBot;

/// <summary>
/// Default greedy strategy:
/// <list type="bullet">
///   <item><b>PlacePhase</b>: place the piece on the first free tile valid for its entry-point type.</item>
///   <item><b>MovePhase</b>: move each piece one step toward the nearest coin.
///   If no coins remain, the piece stays still.</item>
/// </list>
/// Replace this class (or implement <see cref="IStrategy"/>) to customize the bot.
/// </summary>
public sealed class GreedyStrategy : IStrategy
{
    private const int BoardSize = 8;

    /// <summary>
    /// Maps each known piece name to its entry-point type.
    /// Pieces not listed here are treated as "Borders" by default.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> PieceEntryPoints =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // ── Borders ───────────────────────────────────────────────────────
            ["Mickey"]    = "Borders",
            ["Minnie"]    = "Borders",
            ["Elsa"]      = "Borders",
            ["Cogsworth"] = "Borders",
            ["Lumiere"]   = "Borders",
            ["Remy"]      = "Borders",
            ["Anna"]      = "Borders",
            ["Kristoff"]  = "Borders",
            ["Ralph"]     = "Borders",
            ["Pumbaa"]    = "Borders",
            ["WALL•E"]    = "Borders",
            ["Sulley"]    = "Borders",
            ["Stitch"]    = "Borders",
            ["Jafar"]     = "Borders",
            ["Nala"]      = "Borders",
            ["Simba"]     = "Borders",
            ["Oswald"]    = "Borders",
            // ── Corners ───────────────────────────────────────────────────────
            ["Donald"]         = "Corners",
            ["Goofy"]          = "Corners",
            ["Scrooge"]        = "Corners",
            ["Rafiki"]         = "Corners",
            ["Scar"]           = "Corners",
            ["Cinderella"]     = "Corners",
            ["Mike Wazowski"]  = "Corners",
            ["EVE"]            = "Corners",
            // ── Anywhere ─────────────────────────────────────────────────────
            ["Flynn"]           = "Anywhere",
            ["Olaf"]            = "Anywhere",
            ["Daisy"]           = "Anywhere",
            ["Moana"]           = "Anywhere",
            ["Merlin"]          = "Anywhere",
            ["Fairy Godmother"] = "Anywhere",
            ["Ursula"]          = "Anywhere",
            ["Rapunzel"]        = "Anywhere",
            ["Forky"]           = "Anywhere"
        };

    // ── PlacePhase ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public PlacementDecision DecidePlacement(BoardState state, PieceState piece)
    {
        // Collect all positions blocked by obstacles or existing pieces (coins are NOT blocking)
        var occupied = GetOccupiedPositions(state);

        // Choose candidate tiles based on the piece's entry-point type
        var candidates = GetCandidateTiles(piece);

        foreach (var pos in candidates)
        {
            if (occupied.Contains((pos.Row, pos.Col)))
                continue;
            
            Console.WriteLine($"  → Placing {piece.Name} at {pos}");
            return new PlacementDecision.Place(piece.PieceId, pos);
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
                currentPos = step; // Update position for the next segment
                blocked.Add((step.Row, step.Col)); // Treat destination as blocked for subsequent segments
            }
            else
            {
                // Stay still for this segment
                segments.Add([]);
            }
        }

        return new MoveDecision(piece.PieceId, segments.AsReadOnly());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Position? ChooseStep(BoardState state, Position from, PieceState piece, HashSet<(int, int)> blocked)
    {
        if (state.AvailableCoins.Count == 0)
            return null; // No coins — stay still

        // Find the nearest coin (by Euclidean distance)
        var target = state.AvailableCoins
            .OrderBy(c => from.DistanceTo(c.Position))
            .First().Position;

        Console.WriteLine($"    {piece.Name} at {from} → nearest coin at {target}");

        // Generate candidate steps based on a movement type
        var candidates = GetMoveCandidates(from, piece.MovementType);

        // Filter: stay on board, not blocked
        var valid = candidates
            .Where(p => p.Row is >= 0 and < BoardSize && p.Col is >= 0 and < BoardSize)
            .Where(p => !blocked.Contains((p.Row, p.Col)))
            .ToList();

        return valid.Count == 0 ? null :
            // Pick the step closest to the target coin
            valid.OrderBy(p => p.DistanceTo(target)).First();
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
    /// Returns tiles the piece may be placed on, filtered to its entry-point type, and ordered
    /// so that the most preferred tile comes first.
    /// <list type="bullet">
    ///   <item><b>Borders</b>: non-corner border tiles only (a Borders piece cannot use corners).</item>
    ///   <item><b>Corners</b>: the 4 corners first, then non-corner border tiles as fallback.</item>
    ///   <item><b>Anywhere</b>: all board tiles, border first for faster collection.</item>
    /// </list>
    /// </summary>
    private static IEnumerable<Position> GetCandidateTiles(PieceState piece)
    {
        var entryType = PieceEntryPoints.GetValueOrDefault(piece.Name, "Borders");

        var corners = new[]
        {
            new Position(0, 0), new Position(0, BoardSize - 1),
            new Position(BoardSize - 1, 0), new Position(BoardSize - 1, BoardSize - 1)
        };
        var nonCornerBorders = BorderTiles().Except(corners, PositionEqualityComparer.Instance);

        return entryType switch
        {
            "Corners"  => corners.Concat(nonCornerBorders),
            "Anywhere" => AllTiles(),
            _          => nonCornerBorders   // "Borders" and any unknown entry type
        };
    }

    private static IEnumerable<Position> BorderTiles()
    {
        for (var col = 0; col < BoardSize; col++) yield return new Position(0, col);           // top row
        for (var col = 0; col < BoardSize; col++) yield return new Position(BoardSize - 1, col); // bottom row
        for (var row = 1; row < BoardSize - 1; row++) yield return new Position(row, 0);       // left col
        for (var row = 1; row < BoardSize - 1; row++) yield return new Position(row, BoardSize - 1); // right col
    }

    private static IEnumerable<Position> AllTiles()
    {
        // Border tiles first (usually better starting positions), then interior
        foreach (var p in BorderTiles()) yield return p;
        for (var row = 1; row < BoardSize - 1; row++)
            for (var col = 1; col < BoardSize - 1; col++)
                yield return new Position(row, col);
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

    /// <summary>All positions currently occupied by obstacles or pieces (coins are collectible, not blocking).</summary>
    private static HashSet<(int, int)> GetOccupiedPositions(BoardState state) =>
        GetBlockedPositions(state);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private sealed class PositionEqualityComparer : IEqualityComparer<Position>
    {
        public static readonly PositionEqualityComparer Instance = new();
        public bool Equals(Position? x, Position? y) => x?.Row == y?.Row && x?.Col == y?.Col;
        public int GetHashCode(Position p) => HashCode.Combine(p.Row, p.Col);
    }
}
