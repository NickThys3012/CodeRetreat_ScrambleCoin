using MediatR;
using ScrambleCoin.Application.Games.VillainActions.VillainMovePiece;
namespace ScrambleCoin.Application.Games.VillainActions.VillainSkipMovement;

/// <summary>
/// Command for the villain to skip movement during the MovePhase.
/// </summary>
public sealed record VillainSkipMovementCommand(
    Guid GameId,
    Guid VillainPlayerId) : IRequest<VillainMoveResultDto>, IGameStateChangingCommand;
