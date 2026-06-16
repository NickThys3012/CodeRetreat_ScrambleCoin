namespace ScrambleCoin.StarterBot.Models;

// ── Placement decision ────────────────────────────────────────────────────────

/// <summary>
/// The decision made by a strategy for the placement phase of a turn.
/// </summary>
public abstract record PlacementDecision
{
    private PlacementDecision() { }

    /// <summary>Place <paramref name="PieceId"/> at <paramref name="Position"/>.</summary>
    public sealed record Place(Guid PieceId, Position Position) : PlacementDecision;

    /// <summary>Skip placement for this turn.</summary>
    public sealed record Skip : PlacementDecision;
}

// ── Move decision ─────────────────────────────────────────────────────────────

/// <summary>
/// The decision made by a strategy for a single piece during MovePhase.
/// <para>
/// Each piece must submit exactly one <see cref="MoveDecision"/> per MovePhase.
/// <c>Segments</c> must contain exactly <c>MovesPerTurn</c> entries.
/// Each segment is a list of positions the piece steps through during one move action
/// (not including the starting position). An empty inner list means the piece stays still
/// for that action.
/// </para>
/// </summary>
public sealed record MoveDecision(Guid PieceId, IReadOnlyList<IReadOnlyList<Position>> Segments);
