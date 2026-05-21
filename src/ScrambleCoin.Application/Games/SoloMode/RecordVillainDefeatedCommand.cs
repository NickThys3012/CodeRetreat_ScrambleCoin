using MediatR;

namespace ScrambleCoin.Application.Games.SoloMode;

/// <summary>
/// Command to record that a bot defeated a villain and unlocked a piece.
/// This is triggered when a bot wins a solo game.
/// </summary>
public sealed record RecordVillainDefeatedCommand(
    Guid BotId,
    string VillainId,
    string? UnlockedPieceId) : IRequest<RecordVillainDefeatedResult>;

/// <summary>Result of <see cref="RecordVillainDefeatedCommand"/>.</summary>
public sealed record RecordVillainDefeatedResult(
    Guid UnlockId,
    string VillainId,
    string? UnlockedPieceId);
