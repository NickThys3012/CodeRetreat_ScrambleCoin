using ScrambleCoin.Domain.Entities;

namespace ScrambleCoin.Infrastructure.Persistence;

/// <summary>
/// Seeds the default villain DAG on database initialization.
/// This is idempotent and safe to run multiple times.
/// </summary>
public static class VillainTreeSeeder
{
    public static void SeedDefaultTree(ScrambleCoinDbContext context)
    {
        if (context.VillainTreeNodes.Any()) return;

        var nodes = new List<VillainTreeNode>
        {
            Node("stitch",     "Stitch",     null,          order: 1),
            Node("jafar",      "Jafar",      "Goofy",       order: 2),
            Node("elsa",       "Elsa",       "Merlin",      order: 3),
            Node("ursula",     "Ursula",     "Donald",      order: 4),
            Node("maleficent", "Maleficent", "Scrooge",     order: 5),
            Node("gaston",     "Gaston",     "Ralph",       order: 6),
            Node("scar",       "Scar",       "Daisy",       order: 7),
            Node("cruella",    "Cruella",    "Pumbaa",      order: 8)
        };

        context.VillainTreeNodes.AddRange(nodes);
        context.SaveChanges();

        // Parent edges (DAG)
        var parents = new List<VillainNodeParent>
        {
            Edge(child: "elsa",       parent: "stitch"),
            Edge(child: "ursula",     parent: "stitch"),
            Edge(child: "maleficent", parent: "elsa"),
            Edge(child: "gaston",     parent: "ursula"),
            Edge(child: "scar",       parent: "jafar"),
            Edge(child: "cruella",    parent: "maleficent")
        };

        context.VillainNodeParents.AddRange(parents);
        context.SaveChanges();
    }

    private static VillainTreeNode Node(string id, string name, string? piece, int order) => new()
    {
        VillainId      = id,
        VillainName    = name,
        UnlockedPieceId = piece,
        DisplayOrder   = order,
        CreatedAtUtc   = DateTime.UtcNow
    };

    private static VillainNodeParent Edge(string child, string parent) => new()
    {
        ChildVillainId  = child,
        ParentVillainId = parent
    };
}
