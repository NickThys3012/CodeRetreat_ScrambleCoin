using MediatR;

namespace ScrambleCoin.Application.VillainTree;

public sealed record GetVillainTreeQuery : IRequest<VillainTreeDto>;

public sealed record VillainTreeDto(
    IEnumerable<VillainNodeDto> RootNodes,
    IEnumerable<VillainNodeDto> AllNodes
);

public sealed record VillainNodeDto(
    Guid Id,
    string VillainId,
    string VillainName,
    IEnumerable<string> RequiredParentVillainIds,
    string? UnlockedPieceId,
    int DisplayOrder,
    IEnumerable<VillainNodeDto> Children
);
