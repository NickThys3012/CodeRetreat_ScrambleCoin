namespace ScrambleCoin.Application.Games.VillainActions.VillainMovePiece;

/// <summary>
/// Result of a villain movement action.
/// </summary>
public sealed record VillainMoveResultDto(
    string? CurrentPhase,
    string? MovePhaseActivePlayer);
