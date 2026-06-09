using MediatR;
using ScrambleCoin.Application.Games.VillainActions.VillainPlacePiece;
namespace ScrambleCoin.Application.Games.VillainActions.VillainSkipPlacement;

/// <summary>
/// Command for the villain to skip placement during the PlacePhase.
/// </summary>
public sealed record VillainSkipPlacementCommand(
    Guid GameId,
    Guid VillainPlayerId) : IRequest<VillainPlacementResultDto>, IGameStateChangingCommand;
