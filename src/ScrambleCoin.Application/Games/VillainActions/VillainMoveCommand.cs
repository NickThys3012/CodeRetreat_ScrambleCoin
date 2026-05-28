using MediatR;
using ScrambleCoin.Application.Games;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Games.VillainActions;

/// <summary>
/// Command for the villain to move a piece during MovePhase.
/// </summary>
public sealed record VillainMovePieceCommand(
    Guid GameId,
    Guid VillainPlayerId,
    Guid PieceId,
    IReadOnlyList<IReadOnlyList<Position>> Segments) : IRequest<VillainMoveResult>, IGameStateChangingCommand;

/// <summary>
/// Command for the villain to skip movement during the MovePhase.
/// </summary>
public sealed record VillainSkipMovementCommand(
    Guid GameId,
    Guid VillainPlayerId) : IRequest<VillainMoveResult>, IGameStateChangingCommand;

/// <summary>
/// Result of a villain movement action.
/// </summary>
public sealed record VillainMoveResult(
    string? CurrentPhase,
    string? MovePhaseActivePlayer);
