using ScrambleCoin.Domain.Entities;
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
        // a defensive double-check in case a Position was constructed in an
        // unusual way (e.g., reflection).
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
            // A diagonal move from A=(r,c) to B=(r+dr,c+dc) is blocked when two
            // fences form a corner at either intermediate tile:
            //   cornerA = (r+dr, c)  — shares a row with B, a col with A
            //   cornerB = (r, c+dc)  — shares a col with B, a row with A
            //
            // Corner at A: fence A↔cornerA  AND  fence A↔cornerB  → L-shape at A
            // Corner at B: fence B↔cornerA  AND  fence B↔cornerB  → L-shape at B
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
}
