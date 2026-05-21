using MediatR;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Games.VillainActions;

/// <summary>
/// Command for the villain to place a piece during PlacePhase.
/// This is similar to SubmitPlacementCommand but takes the player ID directly instead of a bot token.
/// </summary>
public sealed record VillainPlacePieceCommand(
    Guid GameId,
    Guid VillainPlayerId,
    Guid PieceId,
    Position Position) : IRequest<VillainPlacementResult>;

/// <summary>
/// Command for the villain to skip placement during PlacePhase.
/// </summary>
public sealed record VillainSkipPlacementCommand(
    Guid GameId,
    Guid VillainPlayerId) : IRequest<VillainPlacementResult>;

/// <summary>
/// Result of a villain placement action.
/// </summary>
public sealed record VillainPlacementResult(
    string? CurrentPhase,
    string? MovePhaseActivePlayer);
