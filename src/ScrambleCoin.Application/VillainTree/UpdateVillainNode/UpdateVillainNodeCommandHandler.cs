using MediatR;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;
namespace ScrambleCoin.Application.VillainTree.UpdateVillainNode;

public sealed class UpdateVillainNodeCommandHandler : IRequestHandler<UpdateVillainNodeCommand, Unit>
{
    private readonly IVillainTreeRepository _repository;

    public UpdateVillainNodeCommandHandler(IVillainTreeRepository repository)
    {
        _repository = repository;
    }

    public async Task<Unit> Handle(UpdateVillainNodeCommand request, CancellationToken cancellationToken)
    {
        var node = await _repository.GetNodeByVillainIdAsync(request.VillainId, cancellationToken);
        if (node == null)
            throw new InvalidOperationException($"Villain node '{request.VillainId}' not found");

        node.VillainName = request.VillainName;
        node.UnlockedPieceId = request.UnlockedPieceId;
        node.DisplayOrder = request.DisplayOrder;
        node.ParentLinks = request.RequiredParentVillainIds
            .Select(p => new VillainNodeParent { ChildVillainId = request.VillainId, ParentVillainId = p })
            .ToList();

        await _repository.UpdateNodeAsync(node, cancellationToken);
        return Unit.Value;
    }
}
