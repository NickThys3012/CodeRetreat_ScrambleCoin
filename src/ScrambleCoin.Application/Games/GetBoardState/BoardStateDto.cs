namespace ScrambleCoin.Application.Games.GetBoardState;

/// <summary>
/// Top-level DTO returned by <see cref="GetBoardStateQuery"/>.
/// All fields are relative to the requesting bot (yourScore, yourPieces, etc.).
/// </summary>
/// <param name="Turn">Current turn number (1–5); 0 before game starts.</param>
/// <param name="Phase">Current phase name ("CoinSpawn", "PlacePhase", "MovePhase"), or null if not started.</param>
/// <param name="YourScore">The requesting bot's current score.</param>
/// <param name="OpponentScore">The opponent's current score.</param>
/// <param name="Board">Full 8×8 board with tile details.</param>
/// <param name="YourPieces">All pieces belonging to the requesting bot.</param>
/// <param name="OpponentPieces">All pieces belonging to the opponent.</param>
/// <param name="AvailableCoins">All coin tiles currently on the board.</param>
/// <param name="ActivePlayer">The playerId of the player whose turn it is, or null outside MovePhase.</param>
public sealed record BoardStateDto(
    int Turn,
    string? Phase,
    int YourScore,
    int OpponentScore,
    BoardDto Board,
    IReadOnlyList<PieceDto> YourPieces,
    IReadOnlyList<PieceDto> OpponentPieces,
    IReadOnlyList<CoinDto> AvailableCoins,
    string? ActivePlayer);

/// <summary>The board container: holds the flat list of all 64 tiles.</summary>
/// <param name="Tiles">All tiles on the 8×8 board, in row-major order.</param>
public sealed record BoardDto(IReadOnlyList<TileDto> Tiles);

/// <summary>A single tile on the board.</summary>
/// <param name="Position">Row/col position of this tile.</param>
/// <param name="IsObstacle">True when the tile is covered by a Rock or Lake (impassable).</param>
/// <param name="Occupant">Current occupant (coin or piece info), or null if empty.</param>
/// <param name="FencedEdges">Directions ("North","South","East","West") where a fence blocks movement out of this tile.</param>
public sealed record TileDto(
    PositionDto Position,
    bool IsObstacle,
    TileOccupantDto? Occupant,
    IReadOnlyList<string> FencedEdges);

/// <summary>A position on the board.</summary>
public sealed record PositionDto(int Row, int Col);

/// <summary>
/// The occupant of a tile — discriminated by <see cref="Type"/>.
/// For <c>type = "coin"</c>: <see cref="CoinType"/> and <see cref="Value"/> are set.
/// For <c>type = "piece"</c>: <see cref="PieceId"/>, <see cref="Name"/>, and <see cref="PlayerId"/> are set.
/// </summary>
public sealed record TileOccupantDto(
    string Type,
    string? CoinType = null,
    int? Value = null,
    Guid? PieceId = null,
    string? Name = null,
    Guid? PlayerId = null);

/// <summary>A piece in a player's lineup.</summary>
/// <param name="PieceId">Unique identifier of the piece.</param>
/// <param name="Name">Display name of the piece (e.g. "Mickey").</param>
/// <param name="Position">Current board position, or null if not yet placed.</param>
/// <param name="MovementType">Allowed movement directions ("Orthogonal", "Diagonal", "AnyDirection").</param>
/// <param name="MaxDistance">Maximum tiles per move action.</param>
/// <param name="MovesPerTurn">Number of move actions the piece must perform each turn.</param>
/// <param name="IsOnBoard">True when the piece has been placed on the board.</param>
public sealed record PieceDto(
    Guid PieceId,
    string Name,
    PositionDto? Position,
    string MovementType,
    int MaxDistance,
    int MovesPerTurn,
    bool IsOnBoard);

/// <summary>A coin currently available on the board.</summary>
/// <param name="Position">The tile this coin occupies.</param>
/// <param name="CoinType">Coin type ("Silver" or "Gold").</param>
/// <param name="Value">Point value of the coin.</param>
public sealed record CoinDto(
    PositionDto Position,
    string CoinType,
    int Value);
