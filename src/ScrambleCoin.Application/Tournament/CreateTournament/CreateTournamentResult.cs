namespace ScrambleCoin.Application.Tournament.CreateTournament;

/// <summary>Result returned after a tournament is successfully created.</summary>
/// <param name="TournamentId">The unique identifier of the newly created tournament.</param>
public sealed record CreateTournamentResult(Guid TournamentId);
