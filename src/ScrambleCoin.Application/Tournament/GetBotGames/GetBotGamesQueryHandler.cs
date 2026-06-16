using MediatR;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Tournament.GetBracket;

namespace ScrambleCoin.Application.Tournament.GetBotGames;

/// <summary>
/// Handles <see cref="GetBotGamesQuery"/>: lazily advances the tournament bracket
/// (same as <see cref="GetTournamentBracketQuery"/>) then returns the bot's game IDs and tokens.
/// This ensures bots polling for games will naturally trigger group→knockout advancement
/// without needing a separate bracket call.
/// </summary>
public sealed class GetBotGamesQueryHandler : IRequestHandler<GetBotGamesQuery, IReadOnlyList<BotGameDto>>
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly ISender _sender;

    public GetBotGamesQueryHandler(ITournamentRepository tournamentRepository, ISender sender)
    {
        _tournamentRepository = tournamentRepository;
        _sender = sender;
    }
    public async Task<IReadOnlyList<BotGameDto>> Handle(
        GetBotGamesQuery request, CancellationToken cancellationToken)
    {
        // Trigger lazy bracket advancement (creates knockout games when group stage is done).
        // This is idempotent — safe to call on every poll.
        await _sender.Send(new GetTournamentBracketQuery(request.TournamentId), cancellationToken);

        // Reload fresh from DB — the bracket query may have committed new game assignments,
        // and FindAsync would otherwise return the stale identity-map entity.
        var tournament = await _tournamentRepository.ReloadAsync(request.TournamentId, cancellationToken);

        var results = new List<BotGameDto>();

        // ── Group stage matches ───────────────────────────────────────────────
        foreach (var match in tournament.GroupMatches)
        {
            if (!match.GameId.HasValue) continue;

            var (token, playerId) = match.BotOne == request.BotId
                ? (match.BotOneToken, match.BotOnePlayerId)
                : match.BotTwo == request.BotId
                    ? (match.BotTwoToken, match.BotTwoPlayerId)
                    : (null, null);

            if (token.HasValue && playerId.HasValue)
                results.Add(new BotGameDto(match.Id, "Group", null, match.GameId.Value, token.Value, playerId.Value));
        }

        // ── Knockout matches ──────────────────────────────────────────────────
        foreach (var match in tournament.KnockoutMatches)
        {
            if (!match.GameId.HasValue) continue;

            var (token, playerId) = match.BotOne == request.BotId
                ? (match.BotOneToken, match.BotOnePlayerId)
                : match.BotTwo == request.BotId
                    ? (match.BotTwoToken, match.BotTwoPlayerId)
                    : (null, null);

            if (token.HasValue && playerId.HasValue)
                results.Add(new BotGameDto(match.Id, "Knockout", match.Round, match.GameId.Value, token.Value, playerId.Value));
        }

        return results.AsReadOnly();
    }
}
