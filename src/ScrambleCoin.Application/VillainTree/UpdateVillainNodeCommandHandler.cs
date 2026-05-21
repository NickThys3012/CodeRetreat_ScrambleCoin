using MediatR;
using ScrambleCoin.Application.Interfaces;

namespace ScrambleCoin.Application.VillainTree;

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
        node.RequiredParentVillainId = request.RequiredParentVillainId;
        node.UnlockedPieceId = request.UnlockedPieceId;
        node.DisplayOrder = request.DisplayOrder;

        await _repository.UpdateNodeAsync(node, cancellationToken);
        return Unit.Value;
    }
}
