using MediatR;
namespace ScrambleCoin.Application.VillainTree.UpdateVillainNode;

public sealed record UpdateVillainNodeCommand(
    string VillainId,
    string VillainName,
    IEnumerable<string> RequiredParentVillainIds,
    string? UnlockedPieceId,
    int DisplayOrder
) : IRequest<Unit>;
