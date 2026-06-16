using ScrambleCoin.Application.Games.SubmitPlacement;
namespace ScrambleCoin.Api.RequestBodies;

public static class GameEndpointRequests
{
    // ── Request bodies ────────────────────────────────────────────────────────

    public sealed record JoinGameRequest(IReadOnlyList<string> Lineup);

    public sealed record QueueRequest(IReadOnlyList<string> Lineup);

    /// <summary>
    /// Request body for <c>POST /api/games/{gameId}/move</c>.
    /// </summary>
    /// <param name="PieceId">The piece to move.</param>
    /// <param name="Segments">One segment per MovesPerTurn; each segment is an ordered list of positions.</param>
    public sealed record MoveRequest(Guid PieceId, IReadOnlyList<IReadOnlyList<PositionRequest>>? Segments);

    /// <summary>
    /// Request body for <c>POST /api/games/{gameId}/place</c>.
    /// </summary>
    /// <param name="Action">One of: "place", "replace", "skip".</param>
    /// <param name="PieceId">The piece to place or use as a replacement (required for "place" and "replace").</param>
    /// <param name="ReplacedPieceId">The on-board piece to remove (required for "replace" only).</param>
    /// <param name="Position">Target board position (required for "place" and "replace").</param>
    public sealed record PlacementRequest(
        string? Action,
        Guid? PieceId,
        Guid? ReplacedPieceId,
        PositionRequest? Position);

}
