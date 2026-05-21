using MediatR;

namespace ScrambleCoin.Application.VillainTree;

public sealed record UpdateVillainNodeCommand(
    string VillainId,
    string VillainName,
    string? RequiredParentVillainId,
    string? UnlockedPieceId,
    int DisplayOrder
) : IRequest<Unit>;
