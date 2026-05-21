using ScrambleCoin.Domain.Obstacles;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Entities;

/// <summary>
/// Obstacle management: rocks, lakes, fences, and ice patches.
/// Handles placement, destruction, and queries for all obstacle types.
/// </summary>
public partial class Board
{
    // ── Obstacle management ───────────────────────────────────────────────────

    /// <summary>Adds a <see cref="Rock"/> obstacle to the board.</summary>
    public void AddRock(Rock rock) => _rocks.Add(rock);

    /// <summary>Adds a <see cref="Lake"/> obstacle to the board.</summary>
    public void AddLake(Lake lake) => _lakes.Add(lake);

    /// <summary>Adds a <see cref="Fence"/> obstacle to the board.</summary>
    public void AddFence(Fence fence) => _fences.Add(fence);

    // ── Rock queries ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns <c>true</c> if there is a Rock at the given <paramref name="position"/>.
    /// </summary>
    public bool HasRock(Position position) =>
        _rocks.Any(r => r.Position == position);

    /// <summary>
    /// Removes all Rocks at the given <paramref name="position"/>.
    /// Returns <c>true</c> if at least one Rock was removed; <c>false</c> if no Rock existed.
    /// </summary>
    public bool DestroyRock(Position position)
    {
        var before = _rocks.Count;
        _rocks.RemoveAll(r => r.Position == position);
        return _rocks.Count < before;
    }

    // ── Lake queries ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns <c>true</c> if there is a Lake covering the given <paramref name="position"/>.
    /// </summary>
    public bool HasLake(Position position) =>
        _lakes.Any(l => l.Covers(position));

    /// <summary>
    /// Removes all Lakes covering the given <paramref name="position"/>.
    /// Returns the number of lakes removed.
    /// </summary>
    public int DestroyLake(Position position)
    {
        var before = _lakes.Count;
        _lakes.RemoveAll(l => l.Covers(position));
        return before - _lakes.Count;
    }

    // ── Fence queries ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns <c>true</c> if there is a Fence connected to the given <paramref name="position"/>.
    /// A fence is "connected to" a position if it's on any edge of that tile.
    /// </summary>
    public bool HasFence(Position position)
    {
        // A fence is connected to a position if it's on any of the 4 edges of that tile
        var row = position.Row;
        var col = position.Col;

        var adjacentPositions = new List<Position>();
        if (row > 0) adjacentPositions.Add(new Position(row - 1, col)); // North edge
        if (row < Size - 1) adjacentPositions.Add(new Position(row + 1, col)); // South edge
        if (col > 0) adjacentPositions.Add(new Position(row, col - 1)); // West edge
        if (col < Size - 1) adjacentPositions.Add(new Position(row, col + 1)); // East edge

        return adjacentPositions.Any(adj => _fences.Any(f => f.IsOnEdge(position, adj)));
    }

    /// <summary>
    /// Removes all Fences connected to the given <paramref name="position"/>.
    /// Returns the number of fences removed.
    /// </summary>
    public int DestroyFence(Position position)
    {
        var before = _fences.Count;

        var adjacentPositions = new List<Position>();
        var row = position.Row;
        var col = position.Col;

        if (row > 0) adjacentPositions.Add(new Position(row - 1, col)); // North edge
        if (row < Size - 1) adjacentPositions.Add(new Position(row + 1, col)); // South edge
        if (col > 0) adjacentPositions.Add(new Position(row, col - 1)); // West edge
        if (col < Size - 1) adjacentPositions.Add(new Position(row, col + 1)); // East edge

        // Remove all fences on edges between the position and its adjacent tiles
        _fences.RemoveAll(f =>
            adjacentPositions.Any(adj => f.IsOnEdge(position, adj)));

        return before - _fences.Count;
    }

    // ── Ice patch management ──────────────────────────────────────────────────

    /// <summary>
    /// Returns <c>true</c> if the given <paramref name="position"/> has an ice patch.
    /// </summary>
    public bool HasIcePatch(Position position) => _icePatches.Contains(position);

    /// <summary>
    /// Adds an ice patch to the given <paramref name="position"/>.
    /// If an ice patch already exists at this position, it has no effect (idempotent).
    /// </summary>
    public void PlaceIcePatch(Position position) => _icePatches.Add(position);

    /// <summary>
    /// Returns all positions currently covered by ice patches.
    /// </summary>
    public IReadOnlySet<Position> GetIcePatches() => _icePatches;

    /// <summary>
    /// Removes all ice patches from the board. Used for testing and potential future game rule changes.
    /// </summary>
    public void ClearIcePatches() => _icePatches.Clear();

    /// <summary>
    /// Destroys an ice patch at the given <paramref name="position"/>.
    /// Returns <c>true</c> if an ice patch was removed; <c>false</c> if no ice patch existed.
    /// </summary>
    public bool DestroyIcePatch(Position position) => _icePatches.Remove(position);

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
        new(
            _rocks.AsReadOnly(),
            _lakes.AsReadOnly(),
            _fences.AsReadOnly());

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
