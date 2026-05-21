namespace ScrambleCoin.Domain.Entities;

/// <summary>
/// Represents a node in the villain unlock tree DAG (Directed Acyclic Graph).
/// Each node is a villain that can be challenged by a bot to unlock rewards (pieces).
/// </summary>
public sealed class VillainTreeNode
{
    /// <summary>Unique identifier for this villain node.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Unique villain identifier (e.g., "stitch", "elsa", "maleficent"). Used in APIs.</summary>
    public string VillainId { get; set; } = null!;

    /// <summary>Display name for the villain (e.g., "Stitch", "Elsa", "Maleficent").</summary>
    public string VillainName { get; set; } = null!;

    /// <summary>
    /// The villain ID that must be defeated to unlock this villain.
    /// Null for root villains (no prerequisites).
    /// </summary>
    public string? RequiredParentVillainId { get; set; }

    /// <summary>
    /// The piece ID that is unlocked when this villain is defeated.
    /// Null if this villain awards no piece.
    /// </summary>
    public string? UnlockedPieceId { get; set; }

    /// <summary>Display order for UI rendering.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Timestamp when this villain was created.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
