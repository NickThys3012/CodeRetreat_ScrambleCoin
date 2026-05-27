using ScrambleCoin.Domain.Entities;

namespace ScrambleCoin.Application.Interfaces;

/// <summary>
/// Provides persistence operations for <see cref="VillainTreeNode"/> entities.
/// </summary>
public interface IVillainTreeRepository
{
    /// <summary>Gets all villain tree nodes.</summary>
    Task<IEnumerable<VillainTreeNode>> GetAllNodesAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets a specific villain node by its villain ID.</summary>
    Task<VillainTreeNode?> GetNodeByVillainIdAsync(string villainId, CancellationToken cancellationToken = default);

    /// <summary>Gets all child nodes that require the specified parent villain.</summary>
    Task<IEnumerable<VillainTreeNode>> GetChildrenOfAsync(string parentVillainId, CancellationToken cancellationToken = default);

    /// <summary>Gets all root nodes (villains with no parent requirement).</summary>
    Task<IEnumerable<VillainTreeNode>> GetRootNodesAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds a new villain node to the tree.</summary>
    Task AddNodeAsync(VillainTreeNode node, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing villain node.</summary>
    Task UpdateNodeAsync(VillainTreeNode node, CancellationToken cancellationToken = default);

    /// <summary>Deletes a villain node by its villain ID.</summary>
    Task DeleteNodeAsync(string villainId, CancellationToken cancellationToken = default);
}
