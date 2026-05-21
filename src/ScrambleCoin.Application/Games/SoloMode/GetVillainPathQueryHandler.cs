using MediatR;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Factories;

namespace ScrambleCoin.Application.Games.SoloMode;

/// <summary>
/// Handles <see cref="GetVillainPathQuery"/>.
/// Retrieves the villain tree with lock/unlock status for a specific bot.
/// </summary>
public sealed class GetVillainPathQueryHandler : IRequestHandler<GetVillainPathQuery, GetVillainPathQueryResult>
{
    private readonly IVillainTreeRepository _villainTreeRepository;
    private readonly IBotUnlocksRepository _botUnlocksRepository;

    public GetVillainPathQueryHandler(
        IVillainTreeRepository villainTreeRepository,
        IBotUnlocksRepository botUnlocksRepository)
    {
        _villainTreeRepository = villainTreeRepository;
        _botUnlocksRepository = botUnlocksRepository;
    }

    public async Task<GetVillainPathQueryResult> Handle(GetVillainPathQuery request, CancellationToken cancellationToken)
    {
        var nodes = await _villainTreeRepository.GetAllNodesAsync(cancellationToken);
        var defeatedVillains = await _botUnlocksRepository.GetDefeatedVillainsAsync(request.BotId, cancellationToken);
        var defeatedVillainIds = defeatedVillains.Select(d => d.VillainId).ToHashSet();

        var nodesList = nodes.ToList();
        var dtoNodes = new List<VillainNodeDto>();

        foreach (var node in nodesList)
        {
            // Determine status
            var status = VillainStatusEnum.Locked;
            if (defeatedVillainIds.Contains(node.VillainId))
            {
                status = VillainStatusEnum.Defeated;
            }
            else if (!node.ParentLinks.Any() || node.ParentLinks.All(p => defeatedVillainIds.Contains(p.ParentVillainId)))
            {
                status = VillainStatusEnum.Available;
            }

            // Get children
            var children = await _villainTreeRepository.GetChildrenOfAsync(node.VillainId, cancellationToken);
            var childrenIds = children.Select(c => c.VillainId).ToList();

            // Get unlocked piece
            PieceDto? unlockedPieceDto = null;
            if (!string.IsNullOrEmpty(node.UnlockedPieceId))
            {
                var piece = PieceFactory.TryCreate(node.UnlockedPieceId);
                if (piece != null)
                {
                    unlockedPieceDto = new PieceDto(node.UnlockedPieceId, piece.Name);
                }
            }

            dtoNodes.Add(new VillainNodeDto(
                node.VillainId,
                node.VillainName,
                status,
                unlockedPieceDto,
                childrenIds.AsReadOnly()));
        }

        return new GetVillainPathQueryResult(dtoNodes.AsReadOnly());
    }
}
