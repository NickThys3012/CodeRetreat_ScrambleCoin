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
            ["Elsa"]      = new(EntryPointType.Borders, MovementType.Orthogonal, MaxDistance: 4, MovesPerTurn: 1, AvailableFromTurn: 2),
            // Multistep movement pieces
            ["Cogsworth"] = new(EntryPointType.Borders, MovementType.AnyDirection, MaxDistance: 1, MovesPerTurn: 2, SegmentTypes: [MovementType.AnyDirection, MovementType.Orthogonal], SegmentMaxDistances:
                [1, 2]),
            ["Lumiere"]   = new(EntryPointType.Borders, MovementType.AnyDirection, MaxDistance: 1, MovesPerTurn: 2, SegmentTypes: [MovementType.AnyDirection, MovementType.Diagonal], SegmentMaxDistances:
                [1, 2]),
            ["Remy"]      = new(EntryPointType.Borders, MovementType.Diagonal, MaxDistance: 2, MovesPerTurn: 2, SegmentTypes: [MovementType.Diagonal, MovementType.Diagonal], SegmentMaxDistances: [2, 2
            ], AvailableFromTurn: 2),
            ["Anna"]      = new(EntryPointType.Borders, MovementType.Orthogonal, MaxDistance: 1, MovesPerTurn: 3, SegmentTypes: [MovementType.Orthogonal, MovementType.Orthogonal, MovementType.Orthogonal
            ], SegmentMaxDistances: [1, 1, 1]),
            ["Olaf"]      = new(EntryPointType.Anywhere, MovementType.AnyDirection, MaxDistance: 1, MovesPerTurn: 2, SegmentTypes: [MovementType.AnyDirection, MovementType.AnyDirection], SegmentMaxDistances:
                [1, 1]),
            ["Kristoff"]  = new(EntryPointType.Borders, MovementType.Diagonal, MaxDistance: 1, MovesPerTurn: 3, SegmentTypes: [MovementType.Diagonal, MovementType.Diagonal, MovementType.Diagonal], SegmentMaxDistances:
                [1, 1, 1]),
            
            // On-stop ability pieces (Issue #49)
            ["Ralph"]     = new(EntryPointType.Borders, MovementType.Orthogonal, MaxDistance: 3, MovesPerTurn: 1),
            ["Pumbaa"]    = new(EntryPointType.Borders, MovementType.Charge, MaxDistance: 8, MovesPerTurn: 1),
            ["WALL•E"]    = new(EntryPointType.Borders, MovementType.Charge, MaxDistance: 8, MovesPerTurn: 1),
            ["Sulley"]    = new(EntryPointType.Borders, MovementType.AnyDirection, MaxDistance: 2, MovesPerTurn: 1),
            ["Rafiki"]    = new(EntryPointType.Corners, MovementType.Jump, MaxDistance: 4, MovesPerTurn: 1),
            ["Scar"]      = new(EntryPointType.Corners, MovementType.Jump, MaxDistance: 4, MovesPerTurn: 1, AvailableFromTurn: 3),
            ["Daisy"]     = new(EntryPointType.Anywhere, MovementType.Jump, MaxDistance: 3, MovesPerTurn: 1, AvailableFromTurn: 2),
            ["Stitch"]    = new(EntryPointType.Borders, MovementType.Orthogonal, MaxDistance: 3, MovesPerTurn: 1),

            // Passive & turn-based ability pieces (Issue #50)
            ["Flynn"]           = new(EntryPointType.Anywhere, MovementType.AnyDirection, MaxDistance: 1, MovesPerTurn: 1),
            ["Moana"]           = new(EntryPointType.Anywhere, MovementType.AnyDirection, MaxDistance: 1, MovesPerTurn: 1),
            ["Jafar"]           = new(EntryPointType.Borders, MovementType.AnyDirection, MaxDistance: 2, MovesPerTurn: 1), // Jafar multistep movement (grows)
            ["Merlin"]          = new(EntryPointType.Anywhere, MovementType.Ethereal, MaxDistance: 2, MovesPerTurn: 1, AvailableFromTurn: 4),
            ["Rapunzel"]        = new(EntryPointType.Anywhere, MovementType.AnyDirection, MaxDistance: 1, MovesPerTurn: 1, AvailableFromTurn: 3),
            ["Cinderella"]      = new(EntryPointType.Corners, MovementType.AnyDirection, MaxDistance: 2, MovesPerTurn: 2, SegmentTypes: [MovementType.AnyDirection, MovementType.AnyDirection], SegmentMaxDistances:
                [2, 1]),
            ["Fairy Godmother"] = new(EntryPointType.Anywhere, MovementType.Ethereal, MaxDistance: 2, MovesPerTurn: 1),
            ["Ursula"]          = new(EntryPointType.Anywhere, MovementType.Ethereal, MaxDistance: 2, MovesPerTurn: 1),
            ["Mike Wazowski"]   = new(EntryPointType.Corners, MovementType.AnyDirection, MaxDistance: 2, MovesPerTurn: 1, AvailableFromTurn: 3),
            ["Forky"]           = new(EntryPointType.Anywhere, MovementType.AnyDirection, MaxDistance: 2, MovesPerTurn: 1)
        };

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>All known piece names in the catalogue (sorted alphabetically).</summary>
    public static IReadOnlyList<string> AllPieceNames =>
        Catalogue.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();

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
            template.MovesPerTurn,
            template.AvailableFromTurn);

        // If the template specifies per-segment movement types, set them
        if (template.SegmentTypes is { Length: > 0 })
        {
            piece.SetSegmentMovementTypes(template.SegmentTypes);
        }

        // If the template specifies per-segment max distances, set them
        if (template.SegmentMaxDistances is { Length: > 0 })
        {
            piece.SetSegmentMaxDistances(template.SegmentMaxDistances);
        }

        return piece;
    }

    /// <summary>
    /// Attempts to get a piece by name, returning null if the piece is unknown.
    /// </summary>
    /// <param name="pieceName">The canonical piece name (case-insensitive).</param>
    /// <returns>A <see cref="Piece"/> with a null owner, or null if the piece is unknown.</returns>
    public static Piece? TryCreate(string? pieceName)
    {
        if (string.IsNullOrEmpty(pieceName) || !Catalogue.TryGetValue(pieceName, out var template))
            return null;

        var piece = new Piece(
            pieceName,
            Guid.Empty,
            template.EntryPointType,
            template.MovementType,
            template.MaxDistance,
            template.MovesPerTurn);

        // If the template specifies per-segment movement types, set them
        if (template.SegmentTypes is { Length: > 0 })
        {
            piece.SetSegmentMovementTypes(template.SegmentTypes);
        }

        // If the template specifies per-segment max distances, set them
        if (template.SegmentMaxDistances is { Length: > 0 })
        {
            piece.SetSegmentMaxDistances(template.SegmentMaxDistances);
        }

        return piece;
    }

    /// <summary>Gets the default starter pieces available to all bots.</summary>
    public static IReadOnlyList<string> GetStarterPieces() =>
        ["Mickey", "Minnie", "Donald", "Goofy"];

    // ── Template ──────────────────────────────────────────────────────────────

    private sealed record PieceTemplate(
        EntryPointType EntryPointType,
        MovementType MovementType,
        int MaxDistance,
        int MovesPerTurn,
        MovementType[]? SegmentTypes = null,
        int[]? SegmentMaxDistances = null,
        int? AvailableFromTurn = null);
}
