using MediatR;

namespace ScrambleCoin.Application.Tournament.GetStandings;

/// <summary>
/// Query to retrieve the current group stage standings for a tournament.
/// This query also lazily syncs game results and advances the tournament to the knockout
/// stage when all group games are complete.
/// </summary>
/// <param name="TournamentId">The tournament to retrieve standings for.</param>
public sealed record GetTournamentStandingsQuery(Guid TournamentId) : IRequest<TournamentStandingsDto>;
