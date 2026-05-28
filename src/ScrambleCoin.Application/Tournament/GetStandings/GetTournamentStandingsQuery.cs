using MediatR;

namespace ScrambleCoin.Application.Tournament.GetStandings;

/// <summary>
/// Query to retrieve the current group stage standings for a tournament.
/// This query lazily syncs completed game results into the standings.
/// It does <em>not</em> advance the tournament to the knockout stage —
/// that transition is performed by <see cref="ScrambleCoin.Application.Tournament.GetBracket.GetTournamentBracketQueryHandler"/>.
/// </summary>
/// <param name="TournamentId">The tournament to retrieve standings for.</param>
public sealed record GetTournamentStandingsQuery(Guid TournamentId) : IRequest<TournamentStandingsDto>;
