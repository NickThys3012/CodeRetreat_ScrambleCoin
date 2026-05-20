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
            ["Mickey"]    = new(EntryPointType.Borders, MovementType.Orthogonal,  MaxDistance: 3, MovesPerTurn: 1),
            ["Minnie"]    = new(EntryPointType.Borders, MovementType.Diagonal,    MaxDistance: 3, MovesPerTurn: 1),
            ["Donald"]    = new(EntryPointType.Corners, MovementType.AnyDirection, MaxDistance: 3, MovesPerTurn: 1),
            ["Goofy"]     = new(EntryPointType.Corners, MovementType.Jump, MaxDistance: 3, MovesPerTurn: 1),
            ["Scrooge"]   = new(EntryPointType.Corners, MovementType.AnyDirection, MaxDistance: 2, MovesPerTurn: 1),
            ["Elsa"]      = new(EntryPointType.Borders, MovementType.Orthogonal, MaxDistance: 4, MovesPerTurn: 1),
            // Multi-step movement pieces
            ["Cogsworth"] = new(EntryPointType.Borders, MovementType.AnyDirection, MaxDistance: 1, MovesPerTurn: 2, SegmentTypes: new[] { MovementType.AnyDirection, MovementType.Orthogonal }, SegmentMaxDistances: new[] { 1, 2 }),
            ["Lumiere"]   = new(EntryPointType.Borders, MovementType.AnyDirection, MaxDistance: 1, MovesPerTurn: 2, SegmentTypes: new[] { MovementType.AnyDirection, MovementType.Diagonal }, SegmentMaxDistances: new[] { 1, 2 }),
            ["Remy"]      = new(EntryPointType.Borders, MovementType.Diagonal, MaxDistance: 2, MovesPerTurn: 2, SegmentTypes: new[] { MovementType.Diagonal, MovementType.Diagonal }, SegmentMaxDistances: new[] { 2, 2 }),
            ["Anna"]      = new(EntryPointType.Borders, MovementType.Orthogonal, MaxDistance: 1, MovesPerTurn: 3, SegmentTypes: new[] { MovementType.Orthogonal, MovementType.Orthogonal, MovementType.Orthogonal }, SegmentMaxDistances: new[] { 1, 1, 1 }),
            ["Olaf"]      = new(EntryPointType.Anywhere, MovementType.AnyDirection, MaxDistance: 1, MovesPerTurn: 2, SegmentTypes: new[] { MovementType.AnyDirection, MovementType.AnyDirection }, SegmentMaxDistances: new[] { 1, 1 }),
            ["Kristoff"]  = new(EntryPointType.Borders, MovementType.Diagonal, MaxDistance: 1, MovesPerTurn: 3, SegmentTypes: new[] { MovementType.Diagonal, MovementType.Diagonal, MovementType.Diagonal }, SegmentMaxDistances: new[] { 1, 1, 1 }),
            
            // On-stop ability pieces (Issue #49)
            ["Ralph"]     = new(EntryPointType.Borders, MovementType.Orthogonal, MaxDistance: 3, MovesPerTurn: 1),
            ["Pumbaa"]    = new(EntryPointType.Borders, MovementType.Charge, MaxDistance: 8, MovesPerTurn: 1),
            ["WALL•E"]    = new(EntryPointType.Borders, MovementType.Charge, MaxDistance: 8, MovesPerTurn: 1),
            ["Sulley"]    = new(EntryPointType.Borders, MovementType.AnyDirection, MaxDistance: 2, MovesPerTurn: 1),
            ["Rafiki"]    = new(EntryPointType.Corners, MovementType.Jump, MaxDistance: 4, MovesPerTurn: 1),
            ["Scar"]      = new(EntryPointType.Corners, MovementType.Jump, MaxDistance: 4, MovesPerTurn: 1),
            ["Daisy"]     = new(EntryPointType.Anywhere, MovementType.Jump, MaxDistance: 3, MovesPerTurn: 1),
            ["Stitch"]    = new(EntryPointType.Borders, MovementType.Orthogonal, MaxDistance: 3, MovesPerTurn: 1),
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

        var piece = new Piece(
            pieceName,
            playerId,
            template.EntryPointType,
            template.MovementType,
            template.MaxDistance,
            template.MovesPerTurn);

        // If the template specifies per-segment movement types, set them
        if (template.SegmentTypes != null && template.SegmentTypes.Length > 0)
        {
            piece.SetSegmentMovementTypes(template.SegmentTypes);
        }

        // If the template specifies per-segment max distances, set them
        if (template.SegmentMaxDistances != null && template.SegmentMaxDistances.Length > 0)
        {
            piece.SetSegmentMaxDistances(template.SegmentMaxDistances);
        }

        return piece;
    }

    // ── Template ──────────────────────────────────────────────────────────────

    private sealed record PieceTemplate(
        EntryPointType EntryPointType,
        MovementType MovementType,
        int MaxDistance,
        int MovesPerTurn,
        MovementType[]? SegmentTypes = null,
        int[]? SegmentMaxDistances = null);
}
