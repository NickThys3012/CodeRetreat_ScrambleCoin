using MediatR;
using ScrambleCoin.Domain.ValueObjects;
namespace ScrambleCoin.Application.Games.VillainActions.VillainMovePiece;

/// <summary>
/// Command for the villain to move a piece during MovePhase.
/// </summary>
public sealed record VillainMovePieceCommand(
    Guid GameId,
    Guid VillainPlayerId,
    Guid PieceId,
    IReadOnlyList<IReadOnlyList<Position>> Segments) : IRequest<VillainMoveResultDto>, IGameStateChangingCommand;
