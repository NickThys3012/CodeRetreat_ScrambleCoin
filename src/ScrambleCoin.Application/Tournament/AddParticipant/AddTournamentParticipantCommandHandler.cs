using MediatR;
using Microsoft.Extensions.Logging;

namespace ScrambleCoin.Application.Tournament.AddParticipant;

/// <summary>
/// Handles <see cref="AddTournamentParticipantCommand"/>: registers a bot in the tournament.
/// </summary>
public sealed class AddTournamentParticipantCommandHandler : IRequestHandler<AddTournamentParticipantCommand>
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly ILogger<AddTournamentParticipantCommandHandler> _logger;

    public AddTournamentParticipantCommandHandler(
        ITournamentRepository tournamentRepository,
        ILogger<AddTournamentParticipantCommandHandler> logger)
    {
        _tournamentRepository = tournamentRepository;
        _logger = logger;
    }

    public async Task Handle(AddTournamentParticipantCommand request, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, cancellationToken);

        tournament.AddParticipant(request.BotId, request.BotName, request.Lineup);

        await _tournamentRepository.SaveAsync(tournament, cancellationToken);

        _logger.LogInformation(
            "Bot {BotId} ({BotName}) registered for tournament {TournamentId}",
            request.BotId, request.BotName, request.TournamentId);
    }
}
