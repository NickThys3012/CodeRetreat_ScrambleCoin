namespace ScrambleCoin.Application.VillainTree.GetVillainTree;

public sealed record VillainNodeDto(
    Guid Id,
    string VillainId,
    string VillainName,
    IEnumerable<string> RequiredParentVillainIds,
    string? UnlockedPieceId,
    int DisplayOrder,
    IEnumerable<VillainNodeDto> Children
);

