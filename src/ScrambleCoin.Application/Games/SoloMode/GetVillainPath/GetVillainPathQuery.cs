using MediatR;
namespace ScrambleCoin.Application.Games.SoloMode.GetVillainPath;

/// <summary>
/// Query to get the villain unlock tree for a specific bot, showing which villains are available,
/// locked, or already defeated.
/// </summary>
public sealed record GetVillainPathQuery(Guid BotId) : IRequest<GetVillainPathQueryResult>;

/// <summary>Result of <see cref="GetVillainPathQuery"/>.</summary>
public sealed record GetVillainPathQueryResult(IReadOnlyList<VillainNodeDto> Nodes);
