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
    
