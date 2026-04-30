using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.Obstacles;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Entities;

/// <summary>
/// Holds the result of <see cref="Board.GetAllObstacles"/>.
/// </summary>
public sealed record BoardObstacles(
    IReadOnlyList<Rock> Rocks,
    IReadOnlyList<Lake> Lakes,
    IReadOnlyList<Fence> Fences);

/// <summary>
/// The 8×8 game board. Manages tiles and obstacles; can answer passability queries.
/// </summary>
public sealed class Board
{
    public const int Size = 8;

    // tiles[row, col]
    private readonly Tile[,] _tiles = new Tile[Size, Size];

    private readonly List<Rock> _rocks = [];
    private readonly List<Lake> _lakes = [];
    private readonly List<Fence> _fences = [];

    public Board()
    {
        for (var row = 0; row < Size; row++)
        for (var col = 0; col < Size; col++)
            _tiles[row, col] = new Tile(new Position(row, col));
    }

    // ── Tile access ───────────────────────────────────────────────────────────

    /// <summary>Returns the tile at <paramref name="position"/>.</summary>
    /// <exception cref="DomainException">If the position is out of bounds.</exception>
    public Tile GetTile(Position position)
    {
        // Position already validates bounds in its constructor; this guard is
        // a defensive double-check in case a Position was constructed 
        // unusually (e.g., reflection).
        if (position.Row < 0 || position.Row >= Size ||
            position.Col < 0 || position.Col >= Size)
            throw new DomainException($"Position {position} is out of bounds.");

        return _tiles[position.Row, position.Col];
    }

    // ── Obstacle management ───────────────────────────────────────────────────

    /// <summary>Adds a <see cref="Rock"/> obstacle to the board.</summary>
    public void AddRock(Rock rock) => _rocks.Add(rock);

    /// <summary>Adds a <see cref="Lake"/> obstacle to the board.</summary>
    public void AddLake(Lake lake) => _lakes.Add(lake);

    /// <summary>Adds a <see cref="Fence"/> obstacle to the board.</summary>
    public void AddFence(Fence fence) => _fences.Add(fence);

    // ── Passability ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if a piece can move from <paramref name="from"/> to <paramref name="to"/>.
    /// </summary>
    /// <remarks>
    /// Passability rules:
    /// <list type="bullet">
    ///   <item><description>false if <paramref name="to"/> is a Rock position.</description></item>
    ///   <item><description>false if <paramref name="to"/> is covered by a Lake.</description></item>
    ///   <item><description>false if there is a Fence on the orthogonal edge between the two tiles.</description></item>
    ///   <item><description>For diagonal moves: blocked if two fences form a corner at the intersection.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="DomainException">
    /// Thrown if <paramref name="from"/> and <paramref name="to"/> are not adjacent
    /// (neither orthogonally nor diagonally).
    /// </exception>
    public bool IsPassable(Position from, Position to)
    {
        // Guard: positions must be adjacent (orthogonally or diagonally)
        if (!from.IsOrthogonallyAdjacentTo(to) && !from.IsDiagonallyAdjacentTo(to))
            throw new DomainException($"Positions {from} and {to} are not adjacent.");

        // Blocked by Rock
        if (_rocks.Any(r => r.Position == to))
            return false;

        // Blocked by Lake
        if (_lakes.Any(l => l.Covers(to)))
            return false;

        var rowDiff = to.Row - from.Row;
        var colDiff = to.Col - from.Col;

        var isOrthogonal = (Math.Abs(rowDiff) == 1 && colDiff == 0) ||
                           (rowDiff == 0 && Math.Abs(colDiff) == 1);

        var isDiagonal = Math.Abs(rowDiff) == 1 && Math.Abs(colDiff) == 1;

        if (isOrthogonal)
        {
            // Blocked by a fence directly on this edge
            if (_fences.Any(f => f.IsOnEdge(from, to)))
                return false;
        }
        else if (isDiagonal)
        {
            // A diagonal move from A=(r, c) to B=(r+dr, c+dc) is blocked when two
            // fences form a corner at either intermediate tile:
            //   cornerA = (r+dr, c) — shares a row with B, a col with A
            //   cornerB = (r, c+dc) — shares a col with B, a row with A
            //
            // Corner at A: fence A↔cornerA AND fence A↔cornerB → L-shape at A
            // Corner at B: fence B↔cornerA AND fence B↔cornerB → L-shape at B
            var cornerA = new Position(from.Row + rowDiff, from.Col); // vertically adjacent to from
            var cornerB = new Position(from.Row, from.Col + colDiff); // horizontally adjacent to from

            // Corner at the 'from' side
            var fenceFromVertical   = _fences.Any(f => f.IsOnEdge(from, cornerA));
            var fenceFromHorizontal = _fences.Any(f => f.IsOnEdge(from, cornerB));
            if (fenceFromVertical && fenceFromHorizontal)
                return false;

            // Corner at the 'to' side (symmetric case — blocks *entry* into to)
            var fenceToVertical   = _fences.Any(f => f.IsOnEdge(to, cornerA));
            var fenceToHorizontal = _fences.Any(f => f.IsOnEdge(to, cornerB));
            if (fenceToVertical && fenceToHorizontal)
                return false;
        }

        return true;
    }

    // ── Obstacle coverage ────────────────────────────────────────────────────

    /// <summary>
    /// Returns <c>true</c> if <paramref name="position"/> is covered by a Rock or a Lake.
    /// Fences are on tile edges and are not included.
    /// </summary>
    public bool IsObstacleCovering(Position position) =>
        _rocks.Any(r => r.Position == position) ||
        _lakes.Any(l => l.Covers(position));

    // ── Query helpers ─────────────────────────────────────────────────────────

    /// <summary>Returns all tiles that contain a Coin.</summary>
    public IReadOnlyList<Tile> GetAllCoins()
    {
        var result = new List<Tile>();
        for (var row = 0; row < Size; row++)
        for (var col = 0; col < Size; col++)
        {
            var tile = _tiles[row, col];
            if (tile.AsCoin is not null)
                result.Add(tile);
        }
        return result;
    }

    /// <summary>Returns all tiles that have any occupant (Coin or Piece).</summary>
    public IReadOnlyList<Tile> GetAllOccupiedTiles()
    {
        var result = new List<Tile>();
        for (var row = 0; row < Size; row++)
        for (var col = 0; col < Size; col++)
        {
            var tile = _tiles[row, col];
            if (!tile.IsEmpty)
                result.Add(tile);
        }
        return result;
    }

    /// <summary>Returns all obstacles currently on the board.</summary>
    public BoardObstacles GetAllObstacles() =>
        new BoardObstacles(
            _rocks.AsReadOnly(),
            _lakes.AsReadOnly(),
            _fences.AsReadOnly());

    // ── Movement helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns <c>true</c> if the piece at <paramref name="currentPos"/> has at least one valid move
    /// given its <paramref name="movementType"/>. A move is valid when:
    /// <list type="bullet">
    ///   <item><description>The target position is within board bounds.</description></item>
    ///   <item><description>The edge is passable (<see cref="IsPassable"/>).</description></item>
    ///   <item><description>The target tile is not occupied by another piece.</description></item>
    /// </list>
    /// </summary>
    public bool HasAnyValidMove(Position currentPos, MovementType movementType)
    {
        var deltas = movementType switch
        {
            MovementType.Orthogonal => new[] { (-1, 0), (1, 0), (0, -1), (0, 1) },
            MovementType.Diagonal   => new[] { (-1, -1), (-1, 1), (1, -1), (1, 1) },
            _                       => new[] { (-1, 0), (1, 0), (0, -1), (0, 1),
                                               (-1, -1), (-1, 1), (1, -1), (1, 1) }
        };

        foreach (var (dr, dc) in deltas)
        {
            var newRow = currentPos.Row + dr;
            var newCol = currentPos.Col + dc;

            // Bounds check without constructing Position (which throws on invalid coords).
            if (newRow < 0 || newRow >= Size || newCol < 0 || newCol >= Size)
                continue;

            var neighbor = new Position(newRow, newCol);

            try
            {
                if (!IsPassable(currentPos, neighbor))
                    continue;
            }
            catch
            {
                continue;
            }

            if (_tiles[newRow, newCol].AsPiece is not null)
                continue;

            return true;
        }

        return false;
    }

    // ── Entry-point helpers ───────────────────────────────────────────────────

    /// <summary>Returns <c>true</c> if <paramref name="position"/> is on any board edge (row = 0 or 7, or col = 0 or 7).</summary>
    public bool IsEdgeTile(Position position) =>
        position.Row == 0 || position.Row == Size - 1 ||
        position.Col == 0 || position.Col == Size - 1;

    /// <summary>Returns <c>true</c> if <paramref name="position"/> is one of the 4 corner tiles.</summary>
    public bool IsCornerTile(Position position) =>
        position.Row is 0 or Size - 1 &&
        position.Col is 0 or Size - 1;

    /// <summary>
    /// Returns <c>true</c> if <paramref name="position"/> is a valid entry point for the given
    /// <paramref name="entryPointType"/>.
    /// </summary>
    /// <exception cref="DomainException">Thrown for unknown <see cref="EntryPointType"/> values.</exception>
    public bool IsValidEntryPoint(Position position, EntryPointType entryPointType) =>
        entryPointType switch
        {
            EntryPointType.Borders => IsEdgeTile(position),
            EntryPointType.Corners => IsCornerTile(position),
            EntryPointType.Anywhere => true,
            _ => throw new DomainException($"Unknown EntryPointType: {entryPointType}.")
        };

    /// <summary>
    /// Returns all tiles that are free: empty (no occupant), not covered by a Rock,
    /// and not covered by a Lake. Fences are on tile edges and do not exclude tiles.
    /// </summary>
    public IReadOnlyList<Tile> GetFreeTiles()
    {
        var result = new List<Tile>();
        for (var row = 0; row < Size; row++)
        for (var col = 0; col < Size; col++)
        {
            var tile = _tiles[row, col];
            if (!tile.IsEmpty)
                continue;
            if (IsObstacleCovering(tile.Position))
                continue;
            result.Add(tile);
        }
        return result;
    }
}
