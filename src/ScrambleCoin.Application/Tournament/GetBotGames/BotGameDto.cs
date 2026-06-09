namespace ScrambleCoin.Application.Tournament.GetBotGames;

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
