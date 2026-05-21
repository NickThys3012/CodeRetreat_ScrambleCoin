using MediatR;

namespace ScrambleCoin.Application.VillainTree;

public sealed record DeleteVillainNodeCommand(string VillainId) : IRequest<Unit>;
