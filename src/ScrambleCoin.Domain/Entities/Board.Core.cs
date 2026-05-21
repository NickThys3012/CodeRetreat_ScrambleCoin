using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.Obstacles;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Entities;

/// <summary>
/// Core board state: initialization, tile access, and bounds checking.
/// Handles board construction and basic tile and position queries.
/// </summary>
public partial class Board
{
    public const int Size = 8;

    // tiles[row, col]
    private readonly Tile[,] _tiles = new Tile[Size, Size];

    private readonly List<Rock> _rocks = [];
    private readonly List<Lake> _lakes = [];
    private readonly List<Fence> _fences = [];
    private readonly HashSet<Position> _icePatches = [];

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

    /// <summary>
    /// Returns <c>true</c> if the given <paramref name="position"/> is within board bounds.
    /// </summary>
    public static bool IsWithinBounds(Position position) =>
        position.Row is >= 0 and < Size &&
        position.Col is >= 0 and < Size;

    /// <summary>
    /// Returns <c>true</c> if the tile at <paramref name="position"/> has no occupant (piece or coin).
    /// </summary>
    public bool IsEmpty(Position position) => GetTile(position).IsEmpty;
}
