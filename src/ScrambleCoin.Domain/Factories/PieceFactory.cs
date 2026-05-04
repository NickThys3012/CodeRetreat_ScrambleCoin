using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;

namespace ScrambleCoin.Domain.Factories;

/// <summary>
/// Creates <see cref="Piece"/> instances from well-known piece names.
/// </summary>
/// <remarks>
/// Each starter piece is mapped to its canonical stats as defined in the game overview.
/// Unknown piece names result in a <see cref="DomainException"/>.
/// </remarks>
public static class PieceFactory
{
    // ── Piece catalogue ───────────────────────────────────────────────────────

    private static readonly IReadOnlyDictionary<string, PieceTemplate> Catalogue =
        new Dictionary<string, PieceTemplate>(StringComparer.OrdinalIgnoreCase)
        {
            ["Mickey"]  = new(EntryPointType.Borders, MovementType.Orthogonal,  MaxDistance: 3, MovesPerTurn: 1),
            ["Minnie"]  = new(EntryPointType.Borders, MovementType.Diagonal,    MaxDistance: 3, MovesPerTurn: 1),
            ["Donald"]  = new(EntryPointType.Corners, MovementType.AnyDirection, MaxDistance: 3, MovesPerTurn: 1),
            ["Goofy"]   = new(EntryPointType.Corners, MovementType.AnyDirection, MaxDistance: 3, MovesPerTurn: 1),
            ["Scrooge"] = new(EntryPointType.Corners, MovementType.AnyDirection, MaxDistance: 2, MovesPerTurn: 1),
        };

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="Piece"/> for <paramref name="playerId"/> using the named piece's stats.
    /// </summary>
    /// <param name="pieceName">The canonical piece name (case-insensitive).</param>
    /// <param name="playerId">The owner player identifier.</param>
    /// <returns>A new <see cref="Piece"/> with a freshly generated <see cref="Piece.Id"/>.</returns>
    /// <exception cref="DomainException">Thrown when <paramref name="pieceName"/> is unknown.</exception>
    public static Piece Create(string pieceName, Guid playerId)
    {
        if (!Catalogue.TryGetValue(pieceName, out var template))
            throw new DomainException(
                $"Unknown piece name '{pieceName}'. Known pieces: {string.Join(", ", Catalogue.Keys)}.");

        return new Piece(
            pieceName,
            playerId,
            template.EntryPointType,
            template.MovementType,
            template.MaxDistance,
            template.MovesPerTurn);
    }

    // ── Template ──────────────────────────────────────────────────────────────

    private sealed record PieceTemplate(
        EntryPointType EntryPointType,
        MovementType MovementType,
        int MaxDistance,
        int MovesPerTurn);
}
