using NSubstitute;
using ScrambleCoin.Application.Games.SoloMode;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Unit tests for <see cref="GetVillainPathQueryHandler"/> (Issue #42).
/// Verifies that the villain tree is returned with the correct lock / available / defeated status.
/// </summary>
public class GetVillainPathQueryHandlerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GetVillainPathQueryHandler BuildHandler(
        IVillainTreeRepository villainRepo,
        IBotUnlocksRepository unlocksRepo)
    {
        return new GetVillainPathQueryHandler(villainRepo, unlocksRepo);
    }

    /// <summary>Creates sample villain tree nodes.</summary>
    private static VillainTreeNode[] CreateSampleVillainTree()
    {
        return
        [
            // Root villains (no parent)
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
                VillainId = "jafar",
                VillainName = "Jafar",
                UnlockedPieceId = "Goofy",
                DisplayOrder = 2,
                CreatedAtUtc = DateTime.UtcNow
            },

            // Children of Stitch
            new VillainTreeNode
            {
                Id = Guid.NewGuid(),
                VillainId = "elsa",
                VillainName = "Elsa",
                ParentLinks = [new VillainNodeParent { ChildVillainId = "elsa", ParentVillainId = "stitch" }],
                UnlockedPieceId = "Merlin",
                DisplayOrder = 3,
                CreatedAtUtc = DateTime.UtcNow
            },
            new VillainTreeNode
            {
                Id = Guid.NewGuid(),
                VillainId = "ursula",
                VillainName = "Ursula",
                ParentLinks = [new VillainNodeParent { ChildVillainId = "ursula", ParentVillainId = "stitch" }],
                UnlockedPieceId = "Donald",
                DisplayOrder = 4,
                CreatedAtUtc = DateTime.UtcNow
            },

            // Grandchildren
            new VillainTreeNode
            {
                Id = Guid.NewGuid(),
                VillainId = "maleficent",
                VillainName = "Maleficent",
                ParentLinks = [new VillainNodeParent { ChildVillainId = "maleficent", ParentVillainId = "elsa" }],
                UnlockedPieceId = "Scrooge",
                DisplayOrder = 5,
                CreatedAtUtc = DateTime.UtcNow
            }
        ];
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithNoDefeats_RootsAreAvailable()
    {
        // Arrange
        var botId = Guid.NewGuid();
        var nodes = CreateSampleVillainTree();
        var villainRepo = Substitute.For<IVillainTreeRepository>();
        var unlocksRepo = Substitute.For<IBotUnlocksRepository>();

        villainRepo.GetAllNodesAsync(Arg.Any<CancellationToken>())
            .Returns(nodes);
        villainRepo.GetChildrenOfAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Empty<VillainTreeNode>()));
        unlocksRepo.GetDefeatedVillainsAsync(botId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Empty<BotUnlock>()));

        var handler = BuildHandler(villainRepo, unlocksRepo);
        var query = new GetVillainPathQuery(botId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        var rootStitch = result.Nodes.FirstOrDefault(n => n.VillainId == "stitch");
        var rootJafar = result.Nodes.FirstOrDefault(n => n.VillainId == "jafar");
        
        Assert.NotNull(rootStitch);
        Assert.NotNull(rootJafar);
        Assert.Equal(VillainStatusEnum.Available, rootStitch.Status);
        Assert.Equal(VillainStatusEnum.Available, rootJafar.Status);
    }

    [Fact]
    public async Task Handle_WithNoDefeats_NonRootsAreLocked()
    {
        // Arrange
        var botId = Guid.NewGuid();
        var nodes = CreateSampleVillainTree();
        var villainRepo = Substitute.For<IVillainTreeRepository>();
        var unlocksRepo = Substitute.For<IBotUnlocksRepository>();

        villainRepo.GetAllNodesAsync(Arg.Any<CancellationToken>())
            .Returns(nodes);
        villainRepo.GetChildrenOfAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Empty<VillainTreeNode>()));
        unlocksRepo.GetDefeatedVillainsAsync(botId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Empty<BotUnlock>()));

        var handler = BuildHandler(villainRepo, unlocksRepo);
        var query = new GetVillainPathQuery(botId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        var elsa = result.Nodes.FirstOrDefault(n => n.VillainId == "elsa");
        var ursula = result.Nodes.FirstOrDefault(n => n.VillainId == "ursula");
        var maleficent = result.Nodes.FirstOrDefault(n => n.VillainId == "maleficent");

        Assert.NotNull(elsa);
        Assert.NotNull(ursula);
        Assert.NotNull(maleficent);
        Assert.Equal(VillainStatusEnum.Locked, elsa.Status);
        Assert.Equal(VillainStatusEnum.Locked, ursula.Status);
        Assert.Equal(VillainStatusEnum.Locked, maleficent.Status);
    }

    [Fact]
    public async Task Handle_WithParentDefeated_ChildrenBecomesAvailable()
    {
        // Arrange
        var botId = Guid.NewGuid();
        var nodes = CreateSampleVillainTree();
        var villainRepo = Substitute.For<IVillainTreeRepository>();
        var unlocksRepo = Substitute.For<IBotUnlocksRepository>();

        // Stitch is defeated
        var defeats = new[]
        {
            new BotUnlock
            {
                Id = Guid.NewGuid(),
                BotId = botId,
                VillainId = "stitch",
                UnlockedPieceId = null,
                DefeatedAtUtc = DateTime.UtcNow
            }
        };

        villainRepo.GetAllNodesAsync(Arg.Any<CancellationToken>())
            .Returns(nodes);
        villainRepo.GetChildrenOfAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Empty<VillainTreeNode>()));
        unlocksRepo.GetDefeatedVillainsAsync(botId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(defeats.AsEnumerable()));

        var handler = BuildHandler(villainRepo, unlocksRepo);
        var query = new GetVillainPathQuery(botId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        var stitch = result.Nodes.FirstOrDefault(n => n.VillainId == "stitch");
        var elsa = result.Nodes.FirstOrDefault(n => n.VillainId == "elsa");
        var ursula = result.Nodes.FirstOrDefault(n => n.VillainId == "ursula");

        Assert.NotNull(stitch);
        Assert.NotNull(elsa);
        Assert.NotNull(ursula);
        Assert.Equal(VillainStatusEnum.Defeated, stitch.Status);
        Assert.Equal(VillainStatusEnum.Available, elsa.Status);
        Assert.Equal(VillainStatusEnum.Available, ursula.Status);
    }

    [Fact]
    public async Task Handle_WithVillainDefeated_ReturnsDefeatedStatus()
    {
        // Arrange
        var botId = Guid.NewGuid();
        var nodes = CreateSampleVillainTree();
        var villainRepo = Substitute.For<IVillainTreeRepository>();
        var unlocksRepo = Substitute.For<IBotUnlocksRepository>();

        var defeats = new[]
        {
            new BotUnlock
            {
                Id = Guid.NewGuid(),
                BotId = botId,
                VillainId = "stitch",
                UnlockedPieceId = null,
                DefeatedAtUtc = DateTime.UtcNow
            }
        };

        villainRepo.GetAllNodesAsync(Arg.Any<CancellationToken>())
            .Returns(nodes);
        villainRepo.GetChildrenOfAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Empty<VillainTreeNode>()));
        unlocksRepo.GetDefeatedVillainsAsync(botId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(defeats.AsEnumerable()));

        var handler = BuildHandler(villainRepo, unlocksRepo);
        var query = new GetVillainPathQuery(botId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        var stitch = result.Nodes.FirstOrDefault(n => n.VillainId == "stitch");
        Assert.NotNull(stitch);
        Assert.Equal(VillainStatusEnum.Defeated, stitch.Status);
    }

    [Fact]
    public async Task Handle_WithNoUnlockedPiece_UnlockedPieceIsNull()
    {
        // Arrange
        var botId = Guid.NewGuid();
        var nodes = CreateSampleVillainTree();
        var villainRepo = Substitute.For<IVillainTreeRepository>();
        var unlocksRepo = Substitute.For<IBotUnlocksRepository>();

        villainRepo.GetAllNodesAsync(Arg.Any<CancellationToken>())
            .Returns(nodes);
        villainRepo.GetChildrenOfAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Empty<VillainTreeNode>()));
        unlocksRepo.GetDefeatedVillainsAsync(botId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Empty<BotUnlock>()));

        var handler = BuildHandler(villainRepo, unlocksRepo);
        var query = new GetVillainPathQuery(botId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        var stitch = result.Nodes.FirstOrDefault(n => n.VillainId == "stitch");
        Assert.NotNull(stitch);
        Assert.Null(stitch.UnlockedPiece);
    }

    [Fact]
    public async Task Handle_WithUnlockedPiece_ReturnsPieceDto()
    {
        // Arrange
        var botId = Guid.NewGuid();
        var nodes = CreateSampleVillainTree();
        var villainRepo = Substitute.For<IVillainTreeRepository>();
        var unlocksRepo = Substitute.For<IBotUnlocksRepository>();

        villainRepo.GetAllNodesAsync(Arg.Any<CancellationToken>())
            .Returns(nodes);
        villainRepo.GetChildrenOfAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Empty<VillainTreeNode>()));
        unlocksRepo.GetDefeatedVillainsAsync(botId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Empty<BotUnlock>()));

        var handler = BuildHandler(villainRepo, unlocksRepo);
        var query = new GetVillainPathQuery(botId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        var jafar = result.Nodes.FirstOrDefault(n => n.VillainId == "jafar");
        Assert.NotNull(jafar);
        Assert.NotNull(jafar.UnlockedPiece);
        Assert.Equal("Goofy", jafar.UnlockedPiece.Id);
        Assert.Equal("Goofy", jafar.UnlockedPiece.Name);
    }

    [Fact]
    public async Task Handle_ReturnsChildrenVillainIds()
    {
        // Arrange
        var botId = Guid.NewGuid();
        var nodes = CreateSampleVillainTree();
        var villainRepo = Substitute.For<IVillainTreeRepository>();
        var unlocksRepo = Substitute.For<IBotUnlocksRepository>();

        // Setup: Stitch has Elsa and Ursula as children
        var stitchChildren = new[]
        {
            nodes.First(n => n.VillainId == "elsa"),
            nodes.First(n => n.VillainId == "ursula")
        };

        villainRepo.GetAllNodesAsync(Arg.Any<CancellationToken>())
            .Returns(nodes);
        villainRepo.GetChildrenOfAsync("stitch", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(stitchChildren.AsEnumerable()));
        villainRepo.GetChildrenOfAsync(Arg.Is<string>(x => x != "stitch"), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Empty<VillainTreeNode>()));
        unlocksRepo.GetDefeatedVillainsAsync(botId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Empty<BotUnlock>()));

        var handler = BuildHandler(villainRepo, unlocksRepo);
        var query = new GetVillainPathQuery(botId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        var stitch = result.Nodes.FirstOrDefault(n => n.VillainId == "stitch");
        Assert.NotNull(stitch);
        Assert.Contains("elsa", stitch.ChildrenVillainIds);
        Assert.Contains("ursula", stitch.ChildrenVillainIds);
        Assert.Equal(2, stitch.ChildrenVillainIds.Count);
    }

    [Fact]
    public async Task Handle_WithGrandchildUnlocked_OnlySkipsLockedAncestors()
    {
        // Arrange: Stitch defeated, Elsa defeated → Maleficent should be available
        var botId = Guid.NewGuid();
        var nodes = CreateSampleVillainTree();
        var villainRepo = Substitute.For<IVillainTreeRepository>();
        var unlocksRepo = Substitute.For<IBotUnlocksRepository>();

        var defeats = new[]
        {
            new BotUnlock
            {
                Id = Guid.NewGuid(),
                BotId = botId,
                VillainId = "stitch",
                UnlockedPieceId = null,
                DefeatedAtUtc = DateTime.UtcNow
            },
            new BotUnlock
            {
                Id = Guid.NewGuid(),
                BotId = botId,
                VillainId = "elsa",
                UnlockedPieceId = "Merlin",
                DefeatedAtUtc = DateTime.UtcNow
            }
        };

        villainRepo.GetAllNodesAsync(Arg.Any<CancellationToken>())
            .Returns(nodes);
        villainRepo.GetChildrenOfAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Empty<VillainTreeNode>()));
        unlocksRepo.GetDefeatedVillainsAsync(botId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(defeats.AsEnumerable()));

        var handler = BuildHandler(villainRepo, unlocksRepo);
        var query = new GetVillainPathQuery(botId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        var maleficent = result.Nodes.FirstOrDefault(n => n.VillainId == "maleficent");
        Assert.NotNull(maleficent);
        Assert.Equal(VillainStatusEnum.Available, maleficent.Status);
    }
}
