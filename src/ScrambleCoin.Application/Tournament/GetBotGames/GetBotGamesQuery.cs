using MediatR;

namespace ScrambleCoin.Application.Tournament.GetBotGames;

/// <summary>
/// Query to retrieve game IDs and bot authentication tokens for a specific bot
/// in a tournament. Returns only the requesting bot's own tokens so callers
/// cannot enumerate tokens belonging to other participants.
/// </summary>
/// <param name="TournamentId">The tournament to look up.</param>
/// <param name="BotId">The bot whose matches should be returned.</param>
public sealed record GetBotGamesQuery(Guid TournamentId, Guid BotId)
    : IRequest<IReadOnlyList<BotGameDto>>;

/// <summary>A single match (game + token) that a specific bot must play.</summary>
/// <param name="MatchId">Tournament match identifier.</param>
/// <param name="Stage">Either <c>"Group"</c> or <c>"Knockout"</c>.</param>
/// <param name="Round">Round number — always <c>null</c> for group-stage matches.</param>
/// <param name="GameId">The game identifier to use on the game REST endpoints.</param>
/// <param name="Token">This bot's authentication token for submitting moves.</param>
/// <param name="PlayerId">This bot's in-game player-slot ID (used to check whose turn it is).</param>
public sealed record BotGameDto(
    Guid MatchId,
    string Stage,
    int? Round,
    Guid GameId,
    Guid Token,
    Guid PlayerId);
