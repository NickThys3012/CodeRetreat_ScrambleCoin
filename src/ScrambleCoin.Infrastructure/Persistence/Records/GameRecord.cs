namespace ScrambleCoin.Infrastructure.Persistence.Records;

/// <summary>
/// EF Core persistence POCO for the <see cref="ScrambleCoin.Domain.Entities.Game"/> aggregate.
/// Complex types (Board state, Lineups, Scores, tracking sets) are stored as JSON strings.
/// </summary>
public sealed class GameRecord
{
    public Guid Id { get; set; }
    public Guid PlayerOne { get; set; }
    public Guid PlayerTwo { get; set; }

    /// <summary>Persisted value of <see cref="ScrambleCoin.Domain.Enums.GameStatus"/> cast to int.</summary>
    public int Status { get; set; }

    public int TurnNumber { get; set; }

    /// <summary>Persisted value of <see cref="ScrambleCoin.Domain.Enums.TurnPhase"/> cast to int; null when no active phase.</summary>
    public int? CurrentPhase { get; set; }

    public Guid? MovePhaseActivePlayer { get; set; }

    // ── JSON columns ──────────────────────────────────────────────────────────

    /// <summary>JSON: Dictionary&lt;Guid, int&gt; of player scores.</summary>
    public string ScoresJson { get; set; } = "{}";

    /// <summary>JSON: Dictionary&lt;Guid, int&gt; of pieces each player has on the board.</summary>
    public string PiecesOnBoardJson { get; set; } = "{}";

    /// <summary>JSON: HashSet&lt;Guid&gt; — player IDs that have already acted in the current PlacePhase.</summary>
    public string PlacePhaseDoneJson { get; set; } = "[]";

    /// <summary>JSON: HashSet&lt;Guid&gt; — piece IDs that have already moved in the current MovePhase.</summary>
    public string MovedPieceIdsJson { get; set; } = "[]";

    /// <summary>JSON: array of <see cref="PieceDto"/> — null until PlayerOne submits a lineup.</summary>
    public string? LineupPlayerOneJson { get; set; }

    /// <summary>JSON: array of <see cref="PieceDto"/> — null until PlayerTwo submits a lineup.</summary>
    public string? LineupPlayerTwoJson { get; set; }

    /// <summary>JSON: <see cref="BoardStateDto"/> capturing all obstacles and tile occupants.</summary>
    public string BoardStateJson { get; set; } = "{}";
}
