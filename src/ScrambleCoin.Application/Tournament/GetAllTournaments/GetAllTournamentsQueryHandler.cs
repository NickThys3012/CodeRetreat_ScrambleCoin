using MediatR;
using ScrambleCoin.Application.Interfaces;

namespace ScrambleCoin.Application.Tournament.GetAllTournaments;

/// <summary>
/// Handles <see cref="GetAllTournamentsQuery"/>: loads all tournaments via the repository
/// and projects them to lightweight <see cref="TournamentSummaryDto"/> objects.
/// </summary>
public sealed class GetAllTournamentsQueryHandler
    : IRequestHandler<GetAllTournamentsQuery, IReadOnlyList<TournamentSummaryDto>>
{
    private readonly ITournamentRepository _tournamentRepository;

    public GetAllTournamentsQueryHandler(ITournamentRepository tournamentRepository)
    {
        _tournamentRepository = tournamentRepository;
    }

    public async Task<IReadOnlyList<TournamentSummaryDto>> Handle(
        GetAllTournamentsQuery request,
        CancellationToken cancellationToken)
    {
        var tournaments = await _tournamentRepository.GetAllAsync(cancellationToken);

        return tournaments
            .OrderByDescending(t => t.CreatedAtUtc)
            .Select(t => new TournamentSummaryDto(
                TournamentId:     t.Id,
                Name:             t.Name,
                Status:           t.Status.ToString(),
                ParticipantCount: t.Participants.Count,
                MaxParticipants:  t.MaxParticipants,
                CreatedAtUtc:     t.CreatedAtUtc))
            .ToList()
            .AsReadOnly();
    }
}
