namespace ScrambleCoin.Application.Games.SoloMode.GetVillainPath;

/// <summary>DTO representing a villain node in the tree.</summary>
public sealed record VillainNodeDto(
    string VillainId,
    string Name,
    VillainStatusEnum Status,
    PieceDto? UnlockedPiece,
    IReadOnlyList<string> ChildrenVillainIds);
