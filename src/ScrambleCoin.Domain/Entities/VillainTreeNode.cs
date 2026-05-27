namespace ScrambleCoin.Domain.Entities;

/// <summary>
/// Represents a node in the villain unlocked DAG (Directed Acyclic Graph).
/// A node can have zero, one, or multiple parent villains that must be defeated first.
/// </summary>
public sealed class VillainTreeNode
{
    /// <summary>Unique identifier for this villain node (database PK).</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Unique villain identifier (e.g., "stitch", "elsa"). Used in APIs.</summary>
    public string VillainId { get; set; } = null!;

    /// <summary>Display the name for the villain (e.g. "Stitch", "Elsa").</summary>
    public string VillainName { get; set; } = null!;

    /// <summary>
    /// The piece ID that is unlocked when this villain is defeated.
    /// Null if this villain awards no piece.
    /// </summary>
    public string? UnlockedPieceId { get; set; }

    /// <summary>Display order for UI rendering.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Timestamp when this villain was created.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Parent edges in the DAG — one entry per required-parent villain.
    /// Empty for root villains (no prerequisites).
    /// </summary>
    public ICollection<VillainNodeParent> ParentLinks { get; set; } = new List<VillainNodeParent>();
}
