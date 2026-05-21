using MediatR;
using ScrambleCoin.Application.Interfaces;

namespace ScrambleCoin.Application.VillainTree;

public sealed class GetVillainTreeQueryHandler : IRequestHandler<GetVillainTreeQuery, VillainTreeDto>
{
    private readonly IVillainTreeRepository _repository;

    public GetVillainTreeQueryHandler(IVillainTreeRepository repository)
    {
        _repository = repository;
    }

    public async Task<VillainTreeDto> Handle(GetVillainTreeQuery request, CancellationToken cancellationToken)
    {
        var allNodes = (await _repository.GetAllNodesAsync(cancellationToken)).ToList();
        var rootNodes = (await _repository.GetRootNodesAsync(cancellationToken)).ToList();

        var allNodeDtos = allNodes.Select(n => MapToDto(n, allNodes)).ToList();
        var rootNodeDtos = rootNodes.Select(n => MapToDto(n, allNodes)).ToList();

        return new VillainTreeDto(rootNodeDtos, allNodeDtos);
    }

    private VillainNodeDto MapToDto(Domain.Entities.VillainTreeNode node, List<Domain.Entities.VillainTreeNode> allNodes)
    {
        var children = allNodes
            .Where(n => n.RequiredParentVillainId == node.VillainId)
            .Select(n => MapToDto(n, allNodes))
            .ToList();

        return new VillainNodeDto(
            node.Id,
            node.VillainId,
            node.VillainName,
            node.RequiredParentVillainId,
            node.UnlockedPieceId,
            node.DisplayOrder,
            children
        );
    }
}
