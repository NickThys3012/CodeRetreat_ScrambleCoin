using System.Text.Json.Serialization;

namespace ScrambleCoin.StarterBot.Models;

// ── Board State ──────────────────────────────────────────────────────────────

/// <summary>
/// Full board state returned by GET /api/games/{gameId}/state.
/// All fields are relative to the requesting bot (yourScore, yourPieces, etc.).
/// </summary>
public sealed class BoardState
{
    [JsonPropertyName("turn")]
    public int Turn { get; set; }

    /// <summary>Current phase: "CoinSpawn", "PlacePhase", "MovePhase", or null when the game hasn't started / has ended.</summary>
    [JsonPropertyName("phase")]
    public string? Phase { get; set; }

    [JsonPropertyName("yourScore")]
    public int YourScore { get; set; }

    [JsonPropertyName("opponentScore")]
    public int OpponentScore { get; set; }

    [JsonPropertyName("board")]
    public BoardData Board { get; set; } = new();

    [JsonPropertyName("yourPieces")]
    public List<PieceState> YourPieces { get; set; } = [];

    [JsonPropertyName("opponentPieces")]
    public List<PieceState> OpponentPieces { get; set; } = [];

    [JsonPropertyName("availableCoins")]
    public List<CoinState> AvailableCoins { get; set; } = [];

    /// <summary>The playerId whose turn it is during MovePhase, or null outside MovePhase.</summary>
    [JsonPropertyName("activePlayer")]
    public string? ActivePlayer { get; set; }
}

public sealed class BoardData
{
    [JsonPropertyName("tiles")]
    public List<TileState> Tiles { get; set; } = [];
}

public sealed class TileState
{
    [JsonPropertyName("position")]
    public Position Position { get; set; } = new();

    [JsonPropertyName("isObstacle")]
    public bool IsObstacle { get; set; }

    [JsonPropertyName("occupant")]
    public TileOccupant? Occupant { get; set; }

    [JsonPropertyName("fencedEdges")]
    public List<string> FencedEdges { get; set; } = [];
}

public sealed class TileOccupant
{
    /// <summary>"coin" or "piece"</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    // Coin fields
    [JsonPropertyName("coinType")]
    public string? CoinType { get; set; }

    [JsonPropertyName("value")]
    public int? Value { get; set; }

    // Piece fields
    [JsonPropertyName("pieceId")]
    public Guid? PieceId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("playerId")]
    public Guid? PlayerId { get; set; }
}

public sealed class Position
{
    [JsonPropertyName("row")]
    public int Row { get; set; }

    [JsonPropertyName("col")]
    public int Col { get; set; }

    public Position() { }
    public Position(int row, int col) { Row = row; Col = col; }

    public double DistanceTo(Position other) =>
        Math.Sqrt(Math.Pow(Row - other.Row, 2) + Math.Pow(Col - other.Col, 2));

    public int ManhattanDistanceTo(Position other) =>
        Math.Abs(Row - other.Row) + Math.Abs(Col - other.Col);

    public override string ToString() => $"({Row},{Col})";
}

public sealed class PieceState
{
    [JsonPropertyName("pieceId")]
    public Guid PieceId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("position")]
    public Position? Position { get; set; }

    /// <summary>"Orthogonal", "Diagonal", "AnyDirection", "Jump", "Charge", "Ethereal"</summary>
    [JsonPropertyName("movementType")]
    public string MovementType { get; set; } = "";

    [JsonPropertyName("maxDistance")]
    public int MaxDistance { get; set; }

    [JsonPropertyName("movesPerTurn")]
    public int MovesPerTurn { get; set; }

    [JsonPropertyName("isOnBoard")]
    public bool IsOnBoard { get; set; }
}

public sealed class CoinState
{
    [JsonPropertyName("position")]
    public Position Position { get; set; } = new();

    [JsonPropertyName("coinType")]
    public string CoinType { get; set; } = "";

    [JsonPropertyName("value")]
    public int Value { get; set; }
}

// ── Game Result ───────────────────────────────────────────────────────────────

public sealed class GameResult
{
    [JsonPropertyName("gameId")]
    public Guid GameId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("playerOneId")]
    public Guid PlayerOneId { get; set; }

    [JsonPropertyName("playerOneScore")]
    public int PlayerOneScore { get; set; }

    [JsonPropertyName("playerTwoId")]
    public Guid PlayerTwoId { get; set; }

    [JsonPropertyName("playerTwoScore")]
    public int PlayerTwoScore { get; set; }

    [JsonPropertyName("winnerId")]
    public Guid? WinnerId { get; set; }

    [JsonPropertyName("isDraw")]
    public bool IsDraw { get; set; }
}

// ── Join / Queue ──────────────────────────────────────────────────────────────

public sealed class JoinResponse
{
    [JsonPropertyName("playerId")]
    public Guid PlayerId { get; set; }

    [JsonPropertyName("token")]
    public Guid Token { get; set; }
}

public sealed class QueueResponse
{
    /// <summary>"waiting" or "matched"</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("queueId")]
    public Guid? QueueId { get; set; }

    [JsonPropertyName("gameId")]
    public Guid? GameId { get; set; }

    [JsonPropertyName("playerId")]
    public Guid? PlayerId { get; set; }

    [JsonPropertyName("token")]
    public Guid? Token { get; set; }
}

public sealed class QueuePollResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("gameId")]
    public Guid? GameId { get; set; }

    [JsonPropertyName("playerId")]
    public Guid? PlayerId { get; set; }

    [JsonPropertyName("token")]
    public Guid? Token { get; set; }
}

// ── Move / Placement Responses ─────────────────────────────────────────────

public sealed class PlacementResponse
{
    [JsonPropertyName("phase")]
    public string? Phase { get; set; }

    [JsonPropertyName("activePlayer")]
    public string? ActivePlayer { get; set; }
}

public sealed class MoveResponse
{
    [JsonPropertyName("phase")]
    public string? Phase { get; set; }

    [JsonPropertyName("activePlayer")]
    public string? ActivePlayer { get; set; }

    [JsonPropertyName("yourScore")]
    public int YourScore { get; set; }

    [JsonPropertyName("opponentScore")]
    public int OpponentScore { get; set; }
}
