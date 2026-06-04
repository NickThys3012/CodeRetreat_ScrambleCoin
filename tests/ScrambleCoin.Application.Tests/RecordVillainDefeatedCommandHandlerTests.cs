using NSubstitute;
using ScrambleCoin.Application.Games.SoloMode.RecordVillainDefeated;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Exceptions;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Unit tests for <see cref="RecordVillainDefeatedCommandHandler"/> (Issue #42).
/// Verifies that villain defeats are recorded and pieces are unlocked.
/// </summary>
public class RecordVillainDefeatedCommandHandlerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static RecordVillainDefeatedCommandHandler BuildHandler(
        IBotUnlocksRepository unlocksRepo,
        IVillainTreeRepository villainRepo)
    {
        return new RecordVillainDefeatedCommandHandler(unlocksRepo, villainRepo);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidVillain_RecordsDefeat()
    {
        // Arrange
        var botId = Guid.NewGuid();
        var unlocksRepo = Substitute.For<IBotUnlocksRepository>();
        var villainRepo = Substitute.For<IVillainTreeRepository>();

        var villainNode = new VillainTreeNode
        {
            Id = Guid.NewGuid(),
            VillainId = "stitch",
            VillainName = "Stitch",
            UnlockedPieceId = null,
            DisplayOrder = 1,
            CreatedAtUtc = DateTime.UtcNow
        };

        villainRepo.GetNodeByVillainIdAsync("stitch", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<VillainTreeNode?>(villainNode));

        var handler = BuildHandler(unlocksRepo, villainRepo);
        var command = new RecordVillainDefeatedCommand(botId, "stitch", null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result.UnlockId);
        Assert.Equal("stitch", result.VillainId);
        Assert.Null(result.UnlockedPieceId);
        await unlocksRepo.Received(1).RecordDefeatAsync(botId, "stitch", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithPieceUnlock_IncludesPieceInResult()
    {
        // Arrange
        var botId = Guid.NewGuid();
        var unlocksRepo = Substitute.For<IBotUnlocksRepository>();
        var villainRepo = Substitute.For<IVillainTreeRepository>();

        var villainNode = new VillainTreeNode
        {
            Id = Guid.NewGuid(),
            VillainId = "jafar",
            VillainName = "Jafar",
            UnlockedPieceId = "Goofy",
            DisplayOrder = 2,
            CreatedAtUtc = DateTime.UtcNow
        };

        villainRepo.GetNodeByVillainIdAsync("jafar", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<VillainTreeNode?>(villainNode));

        var handler = BuildHandler(unlocksRepo, villainRepo);
        var command = new RecordVillainDefeatedCommand(botId, "jafar", "Goofy");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result.UnlockId);
        Assert.Equal("jafar", result.VillainId);
        Assert.Equal("Goofy", result.UnlockedPieceId);
        await unlocksRepo.Received(1).RecordDefeatAsync(botId, "jafar", "Goofy", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNonexistentVillain_ThrowsException()
    {
        // Arrange
        var botId = Guid.NewGuid();
        var unlocksRepo = Substitute.For<IBotUnlocksRepository>();
        var villainRepo = Substitute.For<IVillainTreeRepository>();

        villainRepo.GetNodeByVillainIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<VillainTreeNode?>(null));

        var handler = BuildHandler(unlocksRepo, villainRepo);
        var command = new RecordVillainDefeatedCommand(botId, "nonexistent", null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_WithMultipleDefeats_AllRecorded()
    {
        // Arrange
        var botId = Guid.NewGuid();
        var unlocksRepo = Substitute.For<IBotUnlocksRepository>();
        var villainRepo = Substitute.For<IVillainTreeRepository>();

        var stitchNode = new VillainTreeNode
        {
            Id = Guid.NewGuid(),
            VillainId = "stitch",
            VillainName = "Stitch",
            UnlockedPieceId = null,
            DisplayOrder = 1,
            CreatedAtUtc = DateTime.UtcNow
        };

        var elsaNode = new VillainTreeNode
        {
            Id = Guid.NewGuid(),
            VillainId = "elsa",
            VillainName = "Elsa",
            ParentLinks = [new VillainNodeParent { ChildVillainId = "elsa", ParentVillainId = "stitch" }],
            UnlockedPieceId = "Merlin",
            DisplayOrder = 3,
            CreatedAtUtc = DateTime.UtcNow
        };

        villainRepo.GetNodeByVillainIdAsync("stitch", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<VillainTreeNode?>(stitchNode));
        villainRepo.GetNodeByVillainIdAsync("elsa", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<VillainTreeNode?>(elsaNode));

        var handler = BuildHandler(unlocksRepo, villainRepo);

        // Act
        var result1 = await handler.Handle(new RecordVillainDefeatedCommand(botId, "stitch", null), CancellationToken.None);
        var result2 = await handler.Handle(new RecordVillainDefeatedCommand(botId, "elsa", "Merlin"), CancellationToken.None);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal("stitch", result1.VillainId);
        Assert.Equal("elsa", result2.VillainId);
        
        await unlocksRepo.Received(1).RecordDefeatAsync(botId, "stitch", null, Arg.Any<CancellationToken>());
        await unlocksRepo.Received(1).RecordDefeatAsync(botId, "elsa", "Merlin", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReChallengeVillain_AllowsMultipleDefeatRecords()
    {
        // Arrange
        var botId = Guid.NewGuid();
        var unlocksRepo = Substitute.For<IBotUnlocksRepository>();
        var villainRepo = Substitute.For<IVillainTreeRepository>();

        var villainNode = new VillainTreeNode
        {
            Id = Guid.NewGuid(),
            VillainId = "stitch",
            VillainName = "Stitch",
            UnlockedPieceId = null,
            DisplayOrder = 1,
            CreatedAtUtc = DateTime.UtcNow
        };

        villainRepo.GetNodeByVillainIdAsync("stitch", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<VillainTreeNode?>(villainNode));

        var handler = BuildHandler(unlocksRepo, villainRepo);
        var command1 = new RecordVillainDefeatedCommand(botId, "stitch", null);
        var command2 = new RecordVillainDefeatedCommand(botId, "stitch", null);

        // Act
        var result1 = await handler.Handle(command1, CancellationToken.None);
        var result2 = await handler.Handle(command2, CancellationToken.None);

        // Assert: Both should succeed (re-challenge allowed)
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        
        // Verify both calls were made (UPSERT in repository)
        await unlocksRepo.Received(2).RecordDefeatAsync(botId, "stitch", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_GeneratesUniqueUnlockIds()
    {
        // Arrange
        var botId = Guid.NewGuid();
        var unlocksRepo = Substitute.For<IBotUnlocksRepository>();
        var villainRepo = Substitute.For<IVillainTreeRepository>();

        var stitchNode = new VillainTreeNode
        {
            Id = Guid.NewGuid(),
            VillainId = "stitch",
            VillainName = "Stitch",
            UnlockedPieceId = null,
            DisplayOrder = 1,
            CreatedAtUtc = DateTime.UtcNow
        };

        villainRepo.GetNodeByVillainIdAsync("stitch", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<VillainTreeNode?>(stitchNode));

        var handler = BuildHandler(unlocksRepo, villainRepo);
        var command1 = new RecordVillainDefeatedCommand(botId, "stitch", null);
        var command2 = new RecordVillainDefeatedCommand(botId, "stitch", null);

        // Act
        var result1 = await handler.Handle(command1, CancellationToken.None);
        var result2 = await handler.Handle(command2, CancellationToken.None);

        // Assert: Each result should have a unique UnlockId
        Assert.NotEqual(Guid.Empty, result1.UnlockId);
        Assert.NotEqual(Guid.Empty, result2.UnlockId);
        Assert.NotEqual(result1.UnlockId, result2.UnlockId);
    }

    [Fact]
    public async Task Handle_WithNullUnlockedPiece_PassesNullToRepository()
    {
        // Arrange
        var botId = Guid.NewGuid();
        var unlocksRepo = Substitute.For<IBotUnlocksRepository>();
        var villainRepo = Substitute.For<IVillainTreeRepository>();

        var villainNode = new VillainTreeNode
        {
            Id = Guid.NewGuid(),
            VillainId = "stitch",
            VillainName = "Stitch",
            UnlockedPieceId = null,
            DisplayOrder = 1,
            CreatedAtUtc = DateTime.UtcNow
        };

        villainRepo.GetNodeByVillainIdAsync("stitch", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<VillainTreeNode?>(villainNode));

        var handler = BuildHandler(unlocksRepo, villainRepo);
        var command = new RecordVillainDefeatedCommand(botId, "stitch", null);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: Verify null is passed correctly
        await unlocksRepo.Received(1).RecordDefeatAsync(
            botId, 
            "stitch", 
            Arg.Is<string?>(x => x == null), 
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_VerifiesVillainExistsBeforeRecording()
    {
        // Arrange
        var botId = Guid.NewGuid();
        var unlocksRepo = Substitute.For<IBotUnlocksRepository>();
        var villainRepo = Substitute.For<IVillainTreeRepository>();

        villainRepo.GetNodeByVillainIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<VillainTreeNode?>(null));

        var handler = BuildHandler(unlocksRepo, villainRepo);
        var command = new RecordVillainDefeatedCommand(botId, "nonexistent", null);

        // Act
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));

        // Assert: Repository RecordDefeatAsync should NOT be called
        await unlocksRepo.DidNotReceive().RecordDefeatAsync(
            Arg.Any<Guid>(), 
            Arg.Any<string>(), 
            Arg.Any<string?>(), 
            Arg.Any<CancellationToken>());
    }
}
