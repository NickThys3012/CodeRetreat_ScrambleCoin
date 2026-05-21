using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Infrastructure.Persistence;

namespace ScrambleCoin.Infrastructure;

/// <summary>
/// EF Core-backed implementation of <see cref="IVillainTreeRepository"/>.
/// </summary>
public sealed class VillainTreeRepository : IVillainTreeRepository
{
    private readonly ScrambleCoinDbContext _context;

    public VillainTreeRepository(ScrambleCoinDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<VillainTreeNode>> GetAllNodesAsync(CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(
            _context.VillainTreeNodes.OrderBy(v => v.DisplayOrder).ToList());
    }

    public async Task<VillainTreeNode?> GetNodeByVillainIdAsync(string villainId, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(
            _context.VillainTreeNodes.FirstOrDefault(v => v.VillainId == villainId));
    }

    public async Task<IEnumerable<VillainTreeNode>> GetChildrenOfAsync(string parentVillainId, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(
            _context.VillainTreeNodes
                .Where(v => v.RequiredParentVillainId == parentVillainId)
                .OrderBy(v => v.DisplayOrder)
                .ToList());
    }

    public async Task<IEnumerable<VillainTreeNode>> GetRootNodesAsync(CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(
            _context.VillainTreeNodes
                .Where(v => v.RequiredParentVillainId == null)
                .OrderBy(v => v.DisplayOrder)
                .ToList());
    }

    public async Task AddNodeAsync(VillainTreeNode node, CancellationToken cancellationToken = default)
    {
        _context.VillainTreeNodes.Add(node);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateNodeAsync(VillainTreeNode node, CancellationToken cancellationToken = default)
    {
        _context.VillainTreeNodes.Update(node);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteNodeAsync(string villainId, CancellationToken cancellationToken = default)
    {
        var node = await GetNodeByVillainIdAsync(villainId, cancellationToken);
        if (node != null)
        {
            _context.VillainTreeNodes.Remove(node);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
