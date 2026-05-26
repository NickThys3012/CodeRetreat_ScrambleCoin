using Microsoft.EntityFrameworkCore;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Infrastructure.Persistence;

namespace ScrambleCoin.Infrastructure.Tests;

/// <summary>
/// Integration tests for <see cref="VillainTreeRepository"/> (Issue #42).
/// Tests against an in-memory SQLite database.
/// </summary>
public class VillainTreeRepositoryTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static DbContextOptions<ScrambleCoinDbContext> BuildInMemoryOptions(string dbName) =>
        new DbContextOptionsBuilder<ScrambleCoinDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

    private static IEnumerable<VillainTreeNode> CreateSampleNodes()
    {
        return
        [
            new VillainTreeNode
            {
                Id = Guid.NewGuid(),
                VillainId = "stitch",
                VillainName = "Stitch",
                UnlockedPieceId = null,
                DisplayOrder = 1,
                CreatedAtUtc = DateTime.UtcNow
            },
            new VillainTreeNode
            {
                Id = Guid.NewGuid(),
                VillainId = "elsa",
                VillainName = "Elsa",
                ParentLinks = [new VillainNodeParent { ChildVillainId = "elsa", ParentVillainId = "stitch" }],
                UnlockedPieceId = "Merlin",
                DisplayOrder = 2,
                CreatedAtUtc = DateTime.UtcNow
            },
            new VillainTreeNode
            {
                Id = Guid.NewGuid(),
                VillainId = "jafar",
                VillainName = "Jafar",
                UnlockedPieceId = "Goofy",
                DisplayOrder = 3,
                CreatedAtUtc = DateTime.UtcNow
            }
        ];
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllNodesAsync_ReturnsAllNodes()
    {
        // Arrange
        var options = BuildInMemoryOptions(nameof(GetAllNodesAsync_ReturnsAllNodes));
        var nodes = CreateSampleNodes().ToList();

        await using (var context = new ScrambleCoinDbContext(options))
        {
            context.VillainTreeNodes.AddRange(nodes);
            await context.SaveChangesAsync();
        }

        // Act
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var repository = new VillainTreeRepository(context);
            var result = await repository.GetAllNodesAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count());
        }
    }

    [Fact]
    public async Task GetNodeByVillainIdAsync_ReturnsCorrectNode()
    {
        // Arrange
        var options = BuildInMemoryOptions(nameof(GetNodeByVillainIdAsync_ReturnsCorrectNode));
        var nodes = CreateSampleNodes().ToList();

        await using (var context = new ScrambleCoinDbContext(options))
        {
            context.VillainTreeNodes.AddRange(nodes);
            await context.SaveChangesAsync();
        }

        // Act
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var repository = new VillainTreeRepository(context);
            var result = await repository.GetNodeByVillainIdAsync("stitch");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("stitch", result.VillainId);
            Assert.Equal("Stitch", result.VillainName);
            Assert.Empty(result.ParentLinks);
        }
    }

    [Fact]
    public async Task GetNodeByVillainIdAsync_ReturnsNullForNonexistent()
    {
        // Arrange
        var options = BuildInMemoryOptions(nameof(GetNodeByVillainIdAsync_ReturnsNullForNonexistent));
        var nodes = CreateSampleNodes().ToList();

        await using (var context = new ScrambleCoinDbContext(options))
        {
            context.VillainTreeNodes.AddRange(nodes);
            await context.SaveChangesAsync();
        }

        // Act
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var repository = new VillainTreeRepository(context);
            var result = await repository.GetNodeByVillainIdAsync("nonexistent");

            // Assert
            Assert.Null(result);
        }
    }

    [Fact]
    public async Task GetChildrenOfAsync_ReturnsOnlyChildren()
    {
        // Arrange
        var options = BuildInMemoryOptions(nameof(GetChildrenOfAsync_ReturnsOnlyChildren));
        var nodes = CreateSampleNodes().ToList();

        await using (var context = new ScrambleCoinDbContext(options))
        {
            context.VillainTreeNodes.AddRange(nodes);
            await context.SaveChangesAsync();
        }

        // Act
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var repository = new VillainTreeRepository(context);
            var result = await repository.GetChildrenOfAsync("stitch");

            // Assert
            Assert.NotNull(result);
            var resultList = result.ToList();
            Assert.Single(resultList);
            Assert.Equal("elsa", resultList[0].VillainId);
            Assert.Contains(resultList[0].ParentLinks, p => p.ParentVillainId == "stitch");
        }
    }

    [Fact]
    public async Task GetChildrenOfAsync_ReturnsEmptyForNoChildren()
    {
        // Arrange
        var options = BuildInMemoryOptions(nameof(GetChildrenOfAsync_ReturnsEmptyForNoChildren));
        var nodes = CreateSampleNodes().ToList();

        await using (var context = new ScrambleCoinDbContext(options))
        {
            context.VillainTreeNodes.AddRange(nodes);
            await context.SaveChangesAsync();
        }

        // Act
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var repository = new VillainTreeRepository(context);
            var result = await repository.GetChildrenOfAsync("elsa");

            // Assert
            Assert.Empty(result);
        }
    }

    [Fact]
    public async Task GetRootNodesAsync_ReturnsOnlyRoots()
    {
        // Arrange
        var options = BuildInMemoryOptions(nameof(GetRootNodesAsync_ReturnsOnlyRoots));
        var nodes = CreateSampleNodes().ToList();

        await using (var context = new ScrambleCoinDbContext(options))
        {
            context.VillainTreeNodes.AddRange(nodes);
            await context.SaveChangesAsync();
        }

        // Act
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var repository = new VillainTreeRepository(context);
            var result = await repository.GetRootNodesAsync();

            // Assert
            Assert.NotNull(result);
            var resultList = result.ToList();
            Assert.Equal(2, resultList.Count);
            Assert.All(resultList, r => Assert.Empty(r.ParentLinks));
            Assert.Contains(resultList, r => r.VillainId == "stitch");
            Assert.Contains(resultList, r => r.VillainId == "jafar");
        }
    }

    [Fact]
    public async Task AddNodeAsync_PersistsNode()
    {
        // Arrange
        var options = BuildInMemoryOptions(nameof(AddNodeAsync_PersistsNode));
        var newNode = new VillainTreeNode
        {
            Id = Guid.NewGuid(),
            VillainId = "cruella",
            VillainName = "Cruella",
            UnlockedPieceId = "Pumbaa",
            DisplayOrder = 4,
            CreatedAtUtc = DateTime.UtcNow
        };

        // Act
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var repository = new VillainTreeRepository(context);
            await repository.AddNodeAsync(newNode);
        }

        // Assert: Verify persistence
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var retrieved = await context.VillainTreeNodes.FirstOrDefaultAsync(n => n.VillainId == "cruella");
            Assert.NotNull(retrieved);
            Assert.Equal("Cruella", retrieved.VillainName);
        }
    }

    [Fact]
    public async Task UpdateNodeAsync_ModifiesExistingNode()
    {
        // Arrange
        var options = BuildInMemoryOptions(nameof(UpdateNodeAsync_ModifiesExistingNode));
        var node = CreateSampleNodes().First();

        await using (var context = new ScrambleCoinDbContext(options))
        {
            context.VillainTreeNodes.Add(node);
            await context.SaveChangesAsync();
        }

        // Act: Update the node
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var repository = new VillainTreeRepository(context);
            node.VillainName = "Stitch Updated";
            await repository.UpdateNodeAsync(node);
        }

        // Assert: Verify update persisted
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var retrieved = await context.VillainTreeNodes.FirstOrDefaultAsync(n => n.VillainId == "stitch");
            Assert.NotNull(retrieved);
            Assert.Equal("Stitch Updated", retrieved.VillainName);
        }
    }

    [Fact]
    public async Task DeleteNodeAsync_RemovesNode()
    {
        // Arrange
        var options = BuildInMemoryOptions(nameof(DeleteNodeAsync_RemovesNode));
        var nodes = CreateSampleNodes().ToList();

        await using (var context = new ScrambleCoinDbContext(options))
        {
            context.VillainTreeNodes.AddRange(nodes);
            await context.SaveChangesAsync();
        }

        // Act
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var repository = new VillainTreeRepository(context);
            await repository.DeleteNodeAsync("stitch");
        }

        // Assert
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var retrieved = await context.VillainTreeNodes.FirstOrDefaultAsync(n => n.VillainId == "stitch");
            Assert.Null(retrieved);
        }
    }

    [Fact]
    public async Task GetAllNodesAsync_ReturnsOrderedByDisplayOrder()
    {
        // Arrange
        var options = BuildInMemoryOptions(nameof(GetAllNodesAsync_ReturnsOrderedByDisplayOrder));
        var nodes = new[]
        {
            new VillainTreeNode { Id = Guid.NewGuid(), VillainId = "v1", VillainName = "V1", DisplayOrder = 3, CreatedAtUtc = DateTime.UtcNow },
            new VillainTreeNode { Id = Guid.NewGuid(), VillainId = "v2", VillainName = "V2", DisplayOrder = 1, CreatedAtUtc = DateTime.UtcNow },
            new VillainTreeNode { Id = Guid.NewGuid(), VillainId = "v3", VillainName = "V3", DisplayOrder = 2, CreatedAtUtc = DateTime.UtcNow }
        };

        await using (var context = new ScrambleCoinDbContext(options))
        {
            context.VillainTreeNodes.AddRange(nodes);
            await context.SaveChangesAsync();
        }

        // Act
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var repository = new VillainTreeRepository(context);
            var result = await repository.GetAllNodesAsync();

            // Assert: Should be ordered by DisplayOrder
            var resultList = result.ToList();
            Assert.Equal("v2", resultList[0].VillainId);
            Assert.Equal("v3", resultList[1].VillainId);
            Assert.Equal("v1", resultList[2].VillainId);
        }
    }
}
