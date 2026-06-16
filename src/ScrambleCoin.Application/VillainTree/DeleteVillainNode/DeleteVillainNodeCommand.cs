using MediatR;
namespace ScrambleCoin.Application.VillainTree.DeleteVillainNode;

public sealed record DeleteVillainNodeCommand(string VillainId) : IRequest<Unit>;
