using MediatR;
namespace ScrambleCoin.Application.VillainTree.GetVillainTree;

public sealed record GetVillainTreeQuery : IRequest<VillainTreeDto>;

