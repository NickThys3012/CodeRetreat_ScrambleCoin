using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Exceptions;

namespace ScrambleCoin.Domain.ValueObjects;

/// <summary>
/// An immutable, ordered selection of exactly 5 <see cref="Piece"/> instances chosen by a player
/// before the game starts.
/// </summary>
/// <remarks>
/// Equality is order-dependent: two <see cref="Lineup"/> instances are equal only when they contain
/// the same piece Ids in the same order. This mirrors the fact that position within the lineup
/// may be meaningful to the player (e.g. preferred deployment order).
/// </remarks>
public sealed class Lineup : IEquatable<Lineup>
{
    /// <summary>The required number of pieces in a lineup.</summary>
    public const int RequiredPieceCount = 5;

    /// <summary>The ordered list of pieces in this lineup.</summary>
    public IReadOnlyList<Piece> Pieces { get; }

    /// <summary>
    /// Creates a new <see cref="Lineup"/> from the given collection of pieces.
    /// </summary>
    /// <param name="pieces">
    /// Must contain exactly <see cref="RequiredPieceCount"/> non-null pieces with unique Ids.
    /// </param>
    /// <exception cref="DomainException">
    /// Thrown when the collection does not meet the invariants above.
    /// </exception>
    public Lineup(IEnumerable<Piece> pieces)
    {
        if (pieces is null)
            throw new DomainException("Pieces collection must not be null.");

        var list = pieces.ToList();

        if (list.Count != RequiredPieceCount)
            throw new DomainException(
                $"A lineup must contain exactly {RequiredPieceCount} pieces, but {list.Count} were provided.");

        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] is null)
                throw new DomainException($"Piece at index {i} must not be null.");
        }

        var ids = list.Select(p => p.Id).ToList();
        var duplicates = ids.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
            throw new DomainException(
                $"A lineup must not contain duplicate pieces. Duplicate Id(s): {string.Join(", ", duplicates)}.");

        Pieces = list.AsReadOnly();
    }

    /// <inheritdoc/>
    /// <remarks>Equality is order-dependent and based on piece Ids.</remarks>
    public bool Equals(Lineup? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Pieces.Select(p => p.Id).SequenceEqual(other.Pieces.Select(p => p.Id));
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Lineup l && Equals(l);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var piece in Pieces)
            hash.Add(piece.Id);
        return hash.ToHashCode();
    }

    public static bool operator ==(Lineup? left, Lineup? right) =>
        left?.Equals(right) ?? right is null;

    public static bool operator !=(Lineup? left, Lineup? right) => !(left == right);
}
