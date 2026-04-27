using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Entities;

/// <summary>
/// A single cell on the 8×8 board.
/// A tile has a position and an optional occupant (Coin, Piece, or nothing).
/// </summary>
public sealed class Tile
{
    public Position Position { get; }

    /// <summary>
    /// The current occupant of this tile. Can be a <see cref="Coin"/>, a <see cref="Piece"/>, or null.
    /// </summary>
    public object? Occupant { get; private set; }

    public bool IsEmpty => Occupant is null;

    /// <summary>Returns the occupant as a <see cref="Coin"/>, or null if the occupant is not a coin.</summary>
    public Coin? AsCoin => Occupant as Coin;

    /// <summary>Returns the occupant as a <see cref="Piece"/>, or null if the occupant is not a piece.</summary>
    public Piece? AsPiece => Occupant as Piece;

    public Tile(Position position)
    {
        Position = position;
    }

    /// <summary>Places an occupant on this tile, replacing any existing occupant.</summary>
    public void SetOccupant(object? occupant)
    {
        if (occupant is not null)
        {
            var type = occupant.GetType();
            if (type != typeof(Coin) && type != typeof(Piece))
                throw new ArgumentException($"Occupant must be a {nameof(Coin)}, {nameof(Piece)}, or null.", nameof(occupant));
        }

        Occupant = occupant;
    }

    /// <summary>Removes the occupant from this tile.</summary>
    public void ClearOccupant() => Occupant = null;
}
