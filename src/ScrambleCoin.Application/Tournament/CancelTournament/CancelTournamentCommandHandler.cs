using MediatR;
using Microsoft.Extensions.Logging;

namespace ScrambleCoin.Application.Tournament.CancelTournament;

/// <summary>
/// Handles <see cref="CancelTournamentCommand"/>: cancels the tournament.
/// </summary>
public sealed class CancelTournamentCommandHandler : IRequestHandler<CancelTournamentCommand>
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly ILogger<CancelTournamentCommandHandler> _logger;

    public CancelTournamentCommandHandler(
        ITournamentRepository tournamentRepository,
        ILogger<CancelTournamentCommandHandler> logger)
    {
        _tournamentRepository = tournamentRepository;
        _logger = logger;
    }

    public async Task Handle(CancelTournamentCommand request, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, cancellationToken);

        tournament.Cancel();

        await _tournamentRepository.SaveAsync(tournament, cancellationToken);

        _logger.LogInformation("Tournament {TournamentId} cancelled.", request.TournamentId);
    }
}
