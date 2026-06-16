namespace ScrambleCoin.Application.VillainTree.GetVillainTree;

public sealed record VillainTreeDto(
    IEnumerable<VillainNodeDto> RootNodes,
    IEnumerable<VillainNodeDto> AllNodes
);
