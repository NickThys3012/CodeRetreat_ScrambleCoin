using MediatR;
namespace ScrambleCoin.Application.VillainTree.AddVillainNode;

public sealed record AddVillainNodeCommand(
    string VillainId,
    string VillainName,
    IEnumerable<string> RequiredParentVillainIds,
    string? UnlockedPieceId,
    int DisplayOrder
) : IRequest<Guid>;
