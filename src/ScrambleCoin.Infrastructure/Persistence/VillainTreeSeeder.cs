using ScrambleCoin.Domain.Entities;

namespace ScrambleCoin.Infrastructure.Persistence;

/// <summary>
/// Seeds the default villain tree on database initialization.
/// This is idempotent and safe to run multiple times.
/// </summary>
public static class VillainTreeSeeder
{
    /// <summary>
    /// Seeds the default villain tree if no nodes exist yet.
    /// Call this after DbContext migrations in Program.cs or during app initialization.
    /// </summary>
    public static void SeedDefaultTree(ScrambleCoinDbContext context)
    {
        if (context.VillainTreeNodes.Any())
        {
            return; // Already seeded
        }

        var nodes = new List<VillainTreeNode>
        {
            // Root villains
            new()
            {
                Id = Guid.NewGuid(),
                VillainId = "stitch",
                VillainName = "Stitch",
                RequiredParentVillainId = null,
                UnlockedPieceId = null,
                DisplayOrder = 1,
                CreatedAtUtc = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                VillainId = "jafar",
                VillainName = "Jafar",
                RequiredParentVillainId = null,
                UnlockedPieceId = "Goofy",
                DisplayOrder = 2,
                CreatedAtUtc = DateTime.UtcNow
            },

            // Children of Stitch
            new()
            {
                Id = Guid.NewGuid(),
                VillainId = "elsa",
                VillainName = "Elsa",
                RequiredParentVillainId = "stitch",
                UnlockedPieceId = "Merlin",
                DisplayOrder = 3,
                CreatedAtUtc = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                VillainId = "ursula",
                VillainName = "Ursula",
                RequiredParentVillainId = "stitch",
                UnlockedPieceId = "Donald",
                DisplayOrder = 4,
                CreatedAtUtc = DateTime.UtcNow
            },

            // Children of Elsa
            new()
            {
                Id = Guid.NewGuid(),
                VillainId = "maleficent",
                VillainName = "Maleficent",
                RequiredParentVillainId = "elsa",
                UnlockedPieceId = "Scrooge",
                DisplayOrder = 5,
                CreatedAtUtc = DateTime.UtcNow
            },

            // Children of Ursula
            new()
            {
                Id = Guid.NewGuid(),
                VillainId = "gaston",
                VillainName = "Gaston",
                RequiredParentVillainId = "ursula",
                UnlockedPieceId = "Ralph",
                DisplayOrder = 6,
                CreatedAtUtc = DateTime.UtcNow
            },

            // Children of Jafar
            new()
            {
                Id = Guid.NewGuid(),
                VillainId = "scar",
                VillainName = "Scar",
                RequiredParentVillainId = "jafar",
                UnlockedPieceId = "Daisy",
                DisplayOrder = 7,
                CreatedAtUtc = DateTime.UtcNow
            },

            // Additional villain nodes
            new()
            {
                Id = Guid.NewGuid(),
                VillainId = "cruella",
                VillainName = "Cruella",
                RequiredParentVillainId = "maleficent",
                UnlockedPieceId = "Pumbaa",
                DisplayOrder = 8,
                CreatedAtUtc = DateTime.UtcNow
            }
        };

        context.VillainTreeNodes.AddRange(nodes);
        context.SaveChanges();
    }
}
