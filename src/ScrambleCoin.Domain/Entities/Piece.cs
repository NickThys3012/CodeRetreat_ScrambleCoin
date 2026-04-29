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
    /// </summary>
    public int MaxDistance { get; }

    /// <summary>
    /// Number of move actions this piece must perform each turn.
    /// When greater than 1, all moves must be used — partial use is not allowed.
    /// Must be at least 1.
    /// </summary>
    public int MovesPerTurn { get; }

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
    public void PlaceAt(Position position)
    {
        if (position is null)
            throw new DomainException("Position must not be null.");
        Position = position;
    }

    /// <summary>Removes this piece from the board; <see cref="Position"/> becomes <c>null</c>.</summary>
    public void RemoveFromBoard()
    {
        Position = null;
    }
}
