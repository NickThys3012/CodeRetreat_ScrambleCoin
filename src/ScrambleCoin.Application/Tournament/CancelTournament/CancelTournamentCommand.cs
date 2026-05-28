using MediatR;

namespace ScrambleCoin.Application.Tournament.CancelTournament;

/// <summary>
/// Command to cancel an active tournament.
/// Marks all pending games as cancelled (not started or still in progress).
/// </summary>
/// <param name="TournamentId">The tournament to cancel.</param>
public sealed record CancelTournamentCommand(Guid TournamentId) : IRequest;
