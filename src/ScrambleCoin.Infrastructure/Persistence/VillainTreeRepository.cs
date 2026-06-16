using Microsoft.EntityFrameworkCore;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;
namespace ScrambleCoin.Infrastructure.Persistence;

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
        return await _context.VillainTreeNodes
            .Include(v => v.ParentLinks)
            .OrderBy(v => v.DisplayOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<VillainTreeNode?> GetNodeByVillainIdAsync(string villainId, CancellationToken cancellationToken = default)
    {
        return await _context.VillainTreeNodes
            .Include(v => v.ParentLinks)
            .FirstOrDefaultAsync(v => v.VillainId == villainId, cancellationToken);
    }

    public async Task<IEnumerable<VillainTreeNode>> GetChildrenOfAsync(string parentVillainId, CancellationToken cancellationToken = default)
    {
        return await _context.VillainTreeNodes
            .Include(v => v.ParentLinks)
            .Where(v => v.ParentLinks.Any(p => p.ParentVillainId == parentVillainId))
            .OrderBy(v => v.DisplayOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<VillainTreeNode>> GetRootNodesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.VillainTreeNodes
            .Include(v => v.ParentLinks)
            .Where(v => !v.ParentLinks.Any())
            .OrderBy(v => v.DisplayOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task AddNodeAsync(VillainTreeNode node, CancellationToken cancellationToken = default)
    {
        _context.VillainTreeNodes.Add(node);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateNodeAsync(VillainTreeNode node, CancellationToken cancellationToken = default)
    {
        // Replace parent links: remove old, add new (tracked via navigation)
        var existing = await _context.VillainNodeParents
            .Where(p => p.ChildVillainId == node.VillainId)
            .ToListAsync(cancellationToken);
        _context.VillainNodeParents.RemoveRange(existing);

        _context.VillainTreeNodes.Update(node);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteNodeAsync(string villainId, CancellationToken cancellationToken = default)
    {
        var node = await GetNodeByVillainIdAsync(villainId, cancellationToken);
        if (node != null)
        {
            _context.VillainTreeNodes.Remove(node); // cascade deletes ParentLinks
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
