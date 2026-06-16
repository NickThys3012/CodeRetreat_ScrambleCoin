namespace ScrambleCoin.Api.RequestBodies;

public static class TournamentEndpointRequests
{
    
    // ── Request bodies ────────────────────────────────────────────────────────

    /// <summary>Request body for <c>POST /api/tournament</c>.</summary>
    /// <param name="Name">Tournament display name.</param>
    /// <param name="MaxParticipants">Maximum number of bots (minimum 2).</param>
    /// <param name="TopN">Number of group-stage qualifiers for the knockout stage (default: 4).</param>
    public sealed record CreateTournamentRequest(string Name, int MaxParticipants, int? TopN);

    /// <summary>Request body for <c>POST /api/tournament/{id}/participants</c>.</summary>
    /// <param name="BotId">Stable identifier for the bot (used as their game token throughout the tournament).</param>
    /// <param name="BotName">Human-readable display name for this bot.</param>
    /// <param name="Lineup">Ordered list of piece names the bot will use in all their tournament games.</param>
    public sealed record AddParticipantRequest(
        Guid BotId,
        string BotName,
        IReadOnlyList<string> Lineup);
}
