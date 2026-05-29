using MediatR;

namespace ScrambleCoin.Application.Tournament.GetBotGames;

/// <summary>
/// Handles <see cref="GetBotGamesQuery"/>: collects all matches (group and knockout)
/// for a specific bot and returns the game ID and token pairs so the bot can submit moves.
/// </summary>
public sealed class GetBotGamesQueryHandler : IRequestHandler<GetBotGamesQuery, IReadOnlyList<BotGameDto>>
{
    private readonly ITournamentRepository _tournamentRepository;

    public GetBotGamesQueryHandler(ITournamentRepository tournamentRepository)
    {
        _tournamentRepository = tournamentRepository;
    }

    public async Task<IReadOnlyList<BotGameDto>> Handle(
        GetBotGamesQuery request, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, cancellationToken);

        var results = new List<BotGameDto>();

        // ── Group stage matches ───────────────────────────────────────────────
        foreach (var match in tournament.GroupMatches)
        {
            if (!match.GameId.HasValue) continue;

            var token = match.BotOne == request.BotId ? match.BotOneToken
                        : match.BotTwo == request.BotId ? match.BotTwoToken
                        : null;

            if (token.HasValue)
                results.Add(new BotGameDto(match.Id, "Group", null, match.GameId.Value, token.Value));
        }

        // ── Knockout matches ──────────────────────────────────────────────────
        foreach (var match in tournament.KnockoutMatches)
        {
            if (!match.GameId.HasValue) continue;

            var token = match.BotOne == request.BotId ? match.BotOneToken
                        : match.BotTwo == request.BotId ? match.BotTwoToken
                        : null;

            if (token.HasValue)
                results.Add(new BotGameDto(match.Id, "Knockout", match.Round, match.GameId.Value, token.Value));
        }

        return results.AsReadOnly();
    }
}
