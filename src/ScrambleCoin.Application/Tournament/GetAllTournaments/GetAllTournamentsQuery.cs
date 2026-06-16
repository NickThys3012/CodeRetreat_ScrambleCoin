using MediatR;

namespace ScrambleCoin.Application.Tournament.GetAllTournaments;

/// <summary>
/// Query to retrieve a lightweight summary of every tournament in the system.
/// Results are ordered by <c>CreatedAtUtc</c> descending (newest first).
/// </summary>
public sealed record GetAllTournamentsQuery : IRequest<IReadOnlyList<TournamentSummaryDto>>;
