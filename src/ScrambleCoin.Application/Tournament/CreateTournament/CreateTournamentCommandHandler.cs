using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Interfaces;
using DomainTournament = ScrambleCoin.Domain.Tournaments.Tournament;

namespace ScrambleCoin.Application.Tournament.CreateTournament;

/// <summary>
/// Handles <see cref="CreateTournamentCommand"/>: creates and persists a new tournament.
/// </summary>
public sealed class CreateTournamentCommandHandler : IRequestHandler<CreateTournamentCommand, CreateTournamentResult>
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateTournamentCommandHandler> _logger;

    public CreateTournamentCommandHandler(
        ITournamentRepository tournamentRepository,
        IUnitOfWork unitOfWork,
        ILogger<CreateTournamentCommandHandler> logger)
    {
        _tournamentRepository = tournamentRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<CreateTournamentResult> Handle(CreateTournamentCommand request, CancellationToken cancellationToken)
    {
        var tournament = new DomainTournament(
            id: Guid.NewGuid(),
            name: request.Name,
            maxParticipants: request.MaxParticipants,
            topN: request.TopN,
            createdAtUtc: DateTimeOffset.UtcNow);

        await _tournamentRepository.SaveAsync(tournament, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Tournament created: {TournamentId} Name={Name} MaxParticipants={MaxParticipants} TopN={TopN}",
            tournament.Id, tournament.Name, tournament.MaxParticipants, tournament.TopN);

        return new CreateTournamentResult(tournament.Id);
    }
}
