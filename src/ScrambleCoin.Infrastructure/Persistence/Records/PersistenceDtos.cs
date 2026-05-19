namespace ScrambleCoin.Infrastructure.Persistence.Records;

// ── JSON transfer objects for Game aggregate persistence ──────────────────────
// All types are internal and live exclusively in the Infrastructure layer.

/// <summary>Serialized representation of a board position.</summary>
internal sealed record PositionDto(int Row, int Col);

/// <summary>Serialized representation of a Rock obstacle (single tile).</summary>
internal sealed record RockDto(int Row, int Col);

/// <summary>Serialized representation of a Lake obstacle (top-left tile of the 2×2 area).</summary>
internal sealed record LakeDto(int TopLeftRow, int TopLeftCol);

/// <summary>Serialized representation of a Fence obstacle (edge between two orthogonally adjacent tiles).</summary>
internal sealed record FenceDto(int FromRow, int FromCol, int ToRow, int ToCol);

/// <summary>
/// Serialized representation of a single tile occupant.
/// Either a Coin (<see cref="CoinType"/> is set) or a Piece (<see cref="PieceId"/> is set).
/// </summary>
internal sealed record TileOccupantDto(
    int Row,
    int Col,
    bool IsCoin,
    int? CoinType,
    Guid? PieceId);

/// <summary>
/// Serialized snapshot of the full board state:
/// all obstacles and all tile occupants (Coins and Pieces).
/// </summary>
internal sealed record BoardStateDto(
    List<RockDto> Rocks,
    List<LakeDto> Lakes,
    List<FenceDto> Fences,
    List<TileOccupantDto> Occupants);

/// <summary>
/// Serialized representation of a <see cref="ScrambleCoin.Domain.Entities.Piece"/>.
/// Includes the current board position (null when the piece has not yet been placed).
/// </summary>
internal sealed record PieceDto(
    Guid Id,
    string Name,
    Guid PlayerId,
    int EntryPointType,
    int MovementType,
    int MaxDistance,
    int MovesPerTurn,
    int? PositionRow,
    int? PositionCol);
