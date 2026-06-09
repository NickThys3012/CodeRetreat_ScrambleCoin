namespace ScrambleCoin.Application.Games.VillainActions.VillainPlacePiece;

/// <summary>
/// Result of a villain placement action.
/// </summary>
public sealed record VillainPlacementResultDto(
    string? CurrentPhase,
    string? MovePhaseActivePlayer);
