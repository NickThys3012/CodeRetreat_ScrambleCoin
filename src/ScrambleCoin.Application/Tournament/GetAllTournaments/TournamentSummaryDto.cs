namespace ScrambleCoin.Application.Tournament.GetAllTournaments;

/// <summary>
/// Lightweight summary of a tournament for the admin panel list.
/// </summary>
/// <param name="TournamentId">Unique identifier of the tournament.</param>
/// <param name="Name">Human-readable tournament name.</param>
/// <param name="Status">Current lifecycle status (e.g. "Pending", "GroupStage", "Completed").</param>
/// <param name="ParticipantCount">Number of bots currently registered.</param>
/// <param name="MaxParticipants">Maximum allowed participants.</param>
/// <param name="CreatedAtUtc">UTC creation timestamp.</param>
public sealed record TournamentSummaryDto(
    Guid TournamentId,
    string Name,
    string Status,
    int ParticipantCount,
    int MaxParticipants,
    DateTimeOffset CreatedAtUtc);
