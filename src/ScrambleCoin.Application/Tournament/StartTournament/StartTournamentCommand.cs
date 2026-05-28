using MediatR;

namespace ScrambleCoin.Application.Tournament.StartTournament;

/// <summary>
/// Command to start a tournament: locks participants and generates the round-robin schedule.
/// The handler creates all group stage games automatically.
/// </summary>
/// <param name="TournamentId">The tournament to start.</param>
public sealed record StartTournamentCommand(Guid TournamentId) : IRequest;
