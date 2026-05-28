using MediatR;

namespace ScrambleCoin.Application.Tournament.CreateTournament;

/// <summary>
/// Command to create a new named tournament.
/// </summary>
/// <param name="Name">Tournament name (must be unique and non-empty).</param>
/// <param name="MaxParticipants">Maximum number of bots allowed to register.</param>
/// <param name="TopN">Number of top-ranked group-stage bots that advance to knockout. Default: 4.</param>
public sealed record CreateTournamentCommand(
    string Name,
    int MaxParticipants,
    int TopN = 4) : IRequest<CreateTournamentResult>;
