using MediatR;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Games.VillainActions;

/// <summary>
/// Command for the villain to move a piece during MovePhase.
/// </summary>
public sealed record VillainMovePieceCommand(
    Guid GameId,
    Guid VillainPlayerId,
    Guid PieceId,
    IReadOnlyList<IReadOnlyList<Position>> Segments) : IRequest<VillainMoveResult>;

/// <summary>
/// Command for the villain to skip movement during MovePhase.
/// </summary>
public sealed record VillainSkipMovementCommand(
    Guid GameId,
    Guid VillainPlayerId) : IRequest<VillainMoveResult>;

/// <summary>
/// Result of a villain movement action.
/// </summary>
public sealed record VillainMoveResult(
    string? CurrentPhase,
    string? MovePhaseActivePlayer);
