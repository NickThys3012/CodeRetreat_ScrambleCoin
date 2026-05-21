using MediatR;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;

namespace ScrambleCoin.Application.VillainTree;

public sealed class AddVillainNodeCommandHandler : IRequestHandler<AddVillainNodeCommand, Guid>
{
    private readonly IVillainTreeRepository _repository;

    public AddVillainNodeCommandHandler(IVillainTreeRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> Handle(AddVillainNodeCommand request, CancellationToken cancellationToken)
    {
        var node = new VillainTreeNode
        {
            VillainId = request.VillainId,
            VillainName = request.VillainName,
            RequiredParentVillainId = request.RequiredParentVillainId,
            UnlockedPieceId = request.UnlockedPieceId,
            DisplayOrder = request.DisplayOrder
        };

        await _repository.AddNodeAsync(node, cancellationToken);
        return node.Id;
    }
}
