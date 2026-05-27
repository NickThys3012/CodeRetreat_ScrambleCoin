using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Entities;

/// <summary>
/// A player's piece that can be placed on the board and moved around.
/// </summary>
public sealed class Piece
{
    /// <summary>Unique identifier for this piece.</summary>
    public Guid Id { get; }

    /// <summary>Display the name of the piece (e.g. "Knight", "Scout").</summary>
    public string Name { get; }

    /// <summary>Identifier of the player who owns this piece.</summary>
    public Guid PlayerId { get; }

    /// <summary>
    /// Current board position; <c>null</c> when the piece has not yet been placed.
    /// </summary>
    public Position? Position { get; private set; }

    /// <summary>Determines which tiles are valid entry points when placing this piece.</summary>
    public EntryPointType EntryPointType { get; }

    /// <summary>Determines the directions in which this piece may move.</summary>
    public MovementType MovementType { get; }

    /// <summary>
    /// Maximum number of tiles this piece may move in a single move action.
    /// The player may choose any distance from 1 up to this value.
    /// Must be at least 1.
    /// Mutable: can be increased by abilities like Moana.
    /// </summary>
    public int MaxDistance { get; private set; }

    /// <summary>
    /// Number of move actions this piece must perform each turn.
    /// When greater than 1, all moves must be used — partial use is not allowed.
    /// Must be at least 1.
    /// Mutable: can be increased by abilities like Jafar.
    /// </summary>
    public int MovesPerTurn { get; private set; }

    /// <summary>
    /// When a piece has multistep movement sequences (MovesPerTurn > 1),
    /// this list specifies the movement type constraint for each segment.
    /// If null or has fewer entries than MovesPerTurn, segments use the primary <see cref="MovementType"/>.
    /// </summary>
    public IReadOnlyList<MovementType>? SegmentMovementTypes { get; private set; }

    /// <summary>
    /// When a piece has multistep movement sequences (MovesPerTurn > 1),
    /// this list specifies the maximum distance for each segment.
    /// If null or has fewer entries than MovesPerTurn, segments use the primary <see cref="MaxDistance"/>.
    /// </summary>
    public IReadOnlyList<int>? SegmentMaxDistances { get; private set; }

    /// <summary>Returns <c>true</c> when the piece has been placed on the board.</summary>
    public bool IsOnBoard => Position is not null;

    /// <param name="id">Unique piece identifier.</param>
    /// <param name="name">Display name — must not be null or whitespace.</param>
    /// <param name="playerId">Owner player identifier.</param>
    /// <param name="entryPointType">Where the piece may be placed when entering the board.</param>
    /// <param name="movementType">Allowed movement directions.</param>
    /// <param name="maxDistance">Maximum tiles per move action; must be ≥ 1.</param>
    /// <param name="movesPerTurn">Move actions per turn; must be ≥ 1.</param>
    public Piece(
        Guid id,
        string name,
        Guid playerId,
        EntryPointType entryPointType,
        MovementType movementType,
        int maxDistance,
        int movesPerTurn)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Piece name must not be null or whitespace.");

        if (maxDistance < 1)
            throw new DomainException($"MaxDistance must be at least 1, but was {maxDistance}.");

        if (movesPerTurn < 1)
            throw new DomainException($"MovesPerTurn must be at least 1, but was {movesPerTurn}.");

        Id = id;
        Name = name;
        PlayerId = playerId;
        EntryPointType = entryPointType;
        MovementType = movementType;
        MaxDistance = maxDistance;
        MovesPerTurn = movesPerTurn;
    }

    /// <summary>
    /// Convenience constructor that generates a new <see cref="Id"/>.
    /// </summary>
    public Piece(
        string name,
        Guid playerId,
        EntryPointType entryPointType,
        MovementType movementType,
        int maxDistance,
        int movesPerTurn)
        : this(Guid.NewGuid(), name, playerId, entryPointType, movementType, maxDistance, movesPerTurn)
    {
    }

    /// <summary>Places this piece at the given board position.</summary>
    /// <exception cref="DomainException">Thrown when <paramref name="position"/> is null.</exception>
    public void PlaceAt(Position position) => Position = position ?? throw new DomainException("Position must not be null.");

    /// <summary>Removes this piece from the board; <see cref="Position"/> becomes <c>null</c>.</summary>
    public void RemoveFromBoard() => Position = null;

    /// <summary>
    /// Sets the per-segment movement type constraints for multistep pieces.
    /// If null, all segments use the primary <see cref="MovementType"/>.
    /// </summary>
    public void SetSegmentMovementTypes(params MovementType[] types)
    {
        if (types is { Length: > 0 })
            SegmentMovementTypes = types.ToList().AsReadOnly();
    }

    /// <summary>
    /// Sets the per-segment maximum distance constraints for multistep pieces.
    /// If null, all segments use the primary <see cref="MaxDistance"/>.
    /// </summary>
    public void SetSegmentMaxDistances(params int[] distances)
    {
        if (distances is { Length: > 0 })
            SegmentMaxDistances = distances.ToList().AsReadOnly();
    }

    /// <summary>
    /// Gets the movement type constraint for the specified segment index.
    /// Returns the per-segment type if set, otherwise the primary <see cref="MovementType"/>.
    /// </summary>
    public MovementType GetSegmentMovementType(int segmentIndex)
    {
        if (SegmentMovementTypes != null && segmentIndex < SegmentMovementTypes.Count)
            return SegmentMovementTypes[segmentIndex];
        return MovementType;
    }

    /// <summary>
    /// Gets the maximum distance constraint for the specified segment index.
    /// Returns the per-segment max distance if set, otherwise the primary <see cref="MaxDistance"/>.
    /// </summary>
    public int GetSegmentMaxDistance(int segmentIndex)
    {
        if (SegmentMaxDistances != null && segmentIndex < SegmentMaxDistances.Count)
            return SegmentMaxDistances[segmentIndex];
        return MaxDistance;
    }

    /// <summary>
    /// Returns <c>true</c> if this piece is Elsa (identified by name).
    /// Elsa pieces leave ice patches on tiles they pass through.
    /// </summary>
    public bool IsElsa => Name.Equals("Elsa", StringComparison.OrdinalIgnoreCase);

    // ── Ability tracking ───────────────────────────────────────────────────────

    /// <summary>
    /// For Forky: tracks whether this piece has moved on its first turn.
    /// Used to determine if auto-removal should happen at the end of the first move.
    /// </summary>
    public bool HasMovedOnFirstTurn { get; private set; }

    /// <summary>
    /// Bonus coin buff applied by Mike Wazowski: +1 coin on next collection.
    /// Decremented when the piece collects a coin.
    /// </summary>
    public int CoinBuffAmount { get; private set; }

    /// <summary>
    /// Temporary move adjustment for current turn (from Fairy Godmother +1 or Ursula −1).
    /// Resets at the start of each turn.
    /// </summary>
    public int TemporaryMoveAdjustment { get; private set; }

    // ── Stat mutation methods ──────────────────────────────────────────────────

    /// <summary>
    /// Increments <see cref="MaxDistance"/> by 1 (used by Moana ability).
    /// No upper limit is enforced in the domain; application layer may cap if needed.
    /// </summary>
    public void IncreaseMaxDistance()
    {
        MaxDistance++;
    }

    /// <summary>
    /// Increments <see cref="MovesPerTurn"/> by 1 (used by Jafar ability).
    /// No upper limit is enforced in the domain; application layer may cap if needed.
    /// </summary>
    public void IncreaseMovesPerTurn()
    {
        MovesPerTurn++;
    }

    /// <summary>
    /// Marks this piece as having moved on its first turn (used by Forky).
    /// </summary>
    public void MarkAsMovedOnFirstTurn()
    {
        HasMovedOnFirstTurn = true;
    }

    /// <summary>
    /// Applies a coin buff to this piece for their next collection (used by Mike Wazowski).
    /// </summary>
    public void ApplyCoinBuff(int amount)
    {
        CoinBuffAmount += amount;
    }

    /// <summary>
    /// Resets coin buff after a collection.
    /// </summary>
    public void ResetCoinBuff()
    {
        CoinBuffAmount = 0;
    }

    /// <summary>
    /// Applies a temporary move adjustment for the current turn (Fairy Godmother +1, Ursula −1).
    /// </summary>
    public void ApplyTemporaryMoveAdjustment(int adjustment)
    {
        TemporaryMoveAdjustment += adjustment;
    }

    /// <summary>
    /// Resets temporary move adjustments at the start of each turn.
    /// </summary>
    public void ResetTemporaryMoveAdjustment()
    {
        TemporaryMoveAdjustment = 0;
    }
}

