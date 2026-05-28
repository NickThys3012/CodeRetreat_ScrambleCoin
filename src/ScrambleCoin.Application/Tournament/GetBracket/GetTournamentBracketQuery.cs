using MediatR;

namespace ScrambleCoin.Application.Tournament.GetBracket;

/// <summary>
/// Query to retrieve the current bracket state (group schedule + knockout bracket) for a tournament.
/// This query also lazily syncs game results.
/// </summary>
/// <param name="TournamentId">The tournament to retrieve the bracket for.</param>
public sealed record GetTournamentBracketQuery(Guid TournamentId) : IRequest<TournamentBracketDto>;
