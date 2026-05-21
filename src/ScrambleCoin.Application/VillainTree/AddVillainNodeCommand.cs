using MediatR;

namespace ScrambleCoin.Application.VillainTree;

public sealed record AddVillainNodeCommand(
    string VillainId,
    string VillainName,
    string? RequiredParentVillainId,
    string? UnlockedPieceId,
    int DisplayOrder
) : IRequest<Guid>;
