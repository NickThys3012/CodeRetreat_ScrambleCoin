using MediatR;
using ScrambleCoin.Application.Interfaces;

namespace ScrambleCoin.Application.VillainTree;

public sealed class DeleteVillainNodeCommandHandler : IRequestHandler<DeleteVillainNodeCommand, Unit>
{
    private readonly IVillainTreeRepository _repository;

    public DeleteVillainNodeCommandHandler(IVillainTreeRepository repository)
    {
        _repository = repository;
    }

    public async Task<Unit> Handle(DeleteVillainNodeCommand request, CancellationToken cancellationToken)
    {
        await _repository.DeleteNodeAsync(request.VillainId, cancellationToken);
        return Unit.Value;
    }
}
