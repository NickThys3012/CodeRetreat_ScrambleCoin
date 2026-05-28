using MediatR;
using Microsoft.Extensions.Logging;
using ScrambleCoin.Application.Interfaces;

namespace ScrambleCoin.Application.Tournament.AddParticipant;

/// <summary>
/// Handles <see cref="AddTournamentParticipantCommand"/>: registers a bot in the tournament.
/// </summary>
public sealed class AddTournamentParticipantCommandHandler : IRequestHandler<AddTournamentParticipantCommand>
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AddTournamentParticipantCommandHandler> _logger;

    public AddTournamentParticipantCommandHandler(
        ITournamentRepository tournamentRepository,
        IUnitOfWork unitOfWork,
        ILogger<AddTournamentParticipantCommandHandler> logger)
    {
        _tournamentRepository = tournamentRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(AddTournamentParticipantCommand request, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, cancellationToken);

        tournament.AddParticipant(request.BotId, request.BotName, request.Lineup);

        await _tournamentRepository.SaveAsync(tournament, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Bot {BotId} ({BotName}) registered for tournament {TournamentId}",
            request.BotId, request.BotName, request.TournamentId);
    }
}
