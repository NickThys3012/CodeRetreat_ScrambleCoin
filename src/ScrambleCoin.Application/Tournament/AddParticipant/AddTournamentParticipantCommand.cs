using MediatR;

namespace ScrambleCoin.Application.Tournament.AddParticipant;

/// <summary>
/// Command to register a bot as a tournament participant.
/// </summary>
/// <param name="TournamentId">The tournament to join.</param>
/// <param name="BotId">Stable identifier for the bot (used as their game token throughout the tournament).</param>
/// <param name="BotName">Human-readable display name for this bot.</param>
/// <param name="Lineup">Ordered list of piece names the bot will use in all their tournament games.</param>
public sealed record AddTournamentParticipantCommand(
    Guid TournamentId,
    Guid BotId,
    string BotName,
    IReadOnlyList<string> Lineup) : IRequest;
