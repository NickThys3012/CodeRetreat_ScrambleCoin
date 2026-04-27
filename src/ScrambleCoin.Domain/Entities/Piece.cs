namespace ScrambleCoin.Domain.Entities;

/// <summary>
/// A player's piece that can occupy a tile.
/// </summary>
public sealed class Piece
{
    public Guid Id { get; }

    /// <summary>The name of the player who owns this piece.</summary>
    public string Owner { get; }

    public Piece(Guid id, string owner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        Id = id;
        Owner = owner;
    }

    public Piece(string owner) : this(Guid.NewGuid(), owner) { }
}
