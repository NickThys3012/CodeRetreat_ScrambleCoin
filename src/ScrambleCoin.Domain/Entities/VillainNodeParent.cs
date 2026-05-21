namespace ScrambleCoin.Domain.Entities;

/// <summary>
/// Join-table row that records a single parent→child edge in the villain DAG.
/// A child node with N parents has N rows here.
/// </summary>
public sealed class VillainNodeParent
{
    /// <summary>The villain that is a prerequisite.</summary>
    public string ParentVillainId { get; set; } = null!;

    /// <summary>The villain that requires the parent to be defeated first.</summary>
    public string ChildVillainId { get; set; } = null!;
}
