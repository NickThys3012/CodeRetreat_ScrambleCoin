using Microsoft.Extensions.Logging;
using NSubstitute;
using ScrambleCoin.Application.Games.SoloMode;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Unit tests for <see cref="CreateSoloGameCommandHandler"/> (Issue #42).
/// Verifies solo game creation and lock validation.
/// </summary>
public class CreateSoloGameCommandHandlerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static CreateSoloGameCommandHandler BuildHandler(
        IGameRepository gameRepo,
        IVillainTreeRepository villainRepo,
        IBotUnlocksRepository unlocksRepo)
    {
        var logger = Substitute.For<ILogger<CreateSoloGameCommandHandler>>();
        return new CreateSoloGameCommandHandler(gameRepo, villainRepo, unlocksRepo, logger);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithAvailableRootVillain_CreatesGameSuccessfully()
    {
        // Arrange
        var botId = Guid.NewGuid();
        var gameRepo = Substitute.For<IGameRepository>();
        var villainRepo = Substitute.For<IVillainTreeRepository>();
        var unlocksRepo = Substitute.For<IBotUnlocksRepository>();

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
        unlocksRepo.GetDefeatedVillainsAsync(botId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Empty<BotUnlock>()));

        var handler = BuildHandler(gameRepo, villainRepo, unlocksRepo);
        var command = new CreateSoloGameCommand(botId, "stitch");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result.GameId);
        Assert.Equal("stitch", result.VillainId);
        Assert.Equal("Solo", result.GameMode);
        await gameRepo.Received(1).SaveAsync(Arg.Is<Game>(g => 
            g.Id == result.GameId && 
            g.VillainId == "stitch" && 
            g.GameMode == GameMode.Solo),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithUndefeatedParent_ThrowsException()
    {
        // Arrange: Elsa requires Stitch to be defeated first
        var botId = Guid.NewGuid();
        var gameRepo = Substitute.For<IGameRepository>();
        var villainRepo = Substitute.For<IVillainTreeRepository>();
        var unlocksRepo = Substitute.For<IBotUnlocksRepository>();

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

        villainRepo.GetNodeByVillainIdAsync("elsa", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<VillainTreeNode?>(elsaNode));
        unlocksRepo.GetDefeatedVillainsAsync(botId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Empty<BotUnlock>())); // No defeats

        var handler = BuildHandler(gameRepo, villainRepo, unlocksRepo);
        var command = new CreateSoloGameCommand(botId, "elsa");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("locked", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stitch", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_WithDefeatedParent_CreatesGameSuccessfully()
    {
        // Arrange: Stitch is defeated, so Elsa is now available
        var botId = Guid.NewGuid();
        var gameRepo = Substitute.For<IGameRepository>();
        var villainRepo = Substitute.For<IVillainTreeRepository>();
        var unlocksRepo = Substitute.For<IBotUnlocksRepository>();

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

        villainRepo.GetNodeByVillainIdAsync("elsa", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<VillainTreeNode?>(elsaNode));
        unlocksRepo.GetDefeatedVillainsAsync(botId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(defeats.AsEnumerable()));

        var handler = BuildHandler(gameRepo, villainRepo, unlocksRepo);
        var command = new CreateSoloGameCommand(botId, "elsa");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result.GameId);
        Assert.Equal("elsa", result.VillainId);
    }

    [Fact]
    public async Task Handle_WithNonexistentVillain_ThrowsException()
    {
        // Arrange
        var botId = Guid.NewGuid();
        var gameRepo = Substitute.For<IGameRepository>();
        var villainRepo = Substitute.For<IVillainTreeRepository>();
        var unlocksRepo = Substitute.For<IBotUnlocksRepository>();

        villainRepo.GetNodeByVillainIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<VillainTreeNode?>(null));

        var handler = BuildHandler(gameRepo, villainRepo, unlocksRepo);
        var command = new CreateSoloGameCommand(botId, "nonexistent");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_CreatedGameHasGameModeSetToSolo()
    {
        // Arrange
        var botId = Guid.NewGuid();
        Game? capturedGame = null;
        
        var gameRepo = Substitute.For<IGameRepository>();
        await gameRepo.SaveAsync(Arg.Do<Game>(g => capturedGame = g), Arg.Any<CancellationToken>());

        var villainRepo = Substitute.For<IVillainTreeRepository>();
        var unlocksRepo = Substitute.For<IBotUnlocksRepository>();

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
        unlocksRepo.GetDefeatedVillainsAsync(botId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Empty<BotUnlock>()));

        var handler = BuildHandler(gameRepo, villainRepo, unlocksRepo);
        var command = new CreateSoloGameCommand(botId, "stitch");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedGame);
        Assert.Equal(GameMode.Solo, capturedGame.GameMode);
        Assert.Equal("stitch", capturedGame.VillainId);
    }

    [Fact]
    public async Task Handle_CreatedGameHasPlayerOneSetToBot()
    {
        // Arrange
        var botId = Guid.NewGuid();
        Game? capturedGame = null;
        
        var gameRepo = Substitute.For<IGameRepository>();
        await gameRepo.SaveAsync(Arg.Do<Game>(g => capturedGame = g), Arg.Any<CancellationToken>());

        var villainRepo = Substitute.For<IVillainTreeRepository>();
        var unlocksRepo = Substitute.For<IBotUnlocksRepository>();

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
        unlocksRepo.GetDefeatedVillainsAsync(botId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Empty<BotUnlock>()));

        var handler = BuildHandler(gameRepo, villainRepo, unlocksRepo);
        var command = new CreateSoloGameCommand(botId, "stitch");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedGame);
        Assert.Equal(botId, capturedGame.PlayerOne);
    }

    [Fact]
    public async Task Handle_CreatedGameHasVillainPlayerId_DeterministicFromVillainId()
    {
        // Arrange
        var botId = Guid.NewGuid();
        Game? capturedGame1 = null;
        Game? capturedGame2 = null;
        
        var gameRepo = Substitute.For<IGameRepository>();
        var callCount = 0;
        await gameRepo.SaveAsync(
            Arg.Do<Game>(g => 
            {
                if (callCount == 0) capturedGame1 = g;
                else capturedGame2 = g;
                callCount++;
            }), 
            Arg.Any<CancellationToken>());

        var villainRepo = Substitute.For<IVillainTreeRepository>();
        var unlocksRepo = Substitute.For<IBotUnlocksRepository>();

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
        unlocksRepo.GetDefeatedVillainsAsync(botId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Empty<BotUnlock>()));

        var handler = BuildHandler(gameRepo, villainRepo, unlocksRepo);
        var command1 = new CreateSoloGameCommand(botId, "stitch");
        var command2 = new CreateSoloGameCommand(botId, "stitch");

        // Act
        await handler.Handle(command1, CancellationToken.None);
        await handler.Handle(command2, CancellationToken.None);

        // Assert: Same villain should produce same villain player ID
        Assert.NotNull(capturedGame1);
        Assert.NotNull(capturedGame2);
        Assert.Equal(capturedGame1.PlayerTwo, capturedGame2.PlayerTwo);
        Assert.NotEqual(Guid.Empty, capturedGame1.PlayerTwo);
    }

    [Fact]
    public async Task Handle_ReChallengeDefeatedVillain_CreatesNewGame()
    {
        // Arrange: Stitch was already defeated, but can be re-challenged
        var botId = Guid.NewGuid();
        var gameRepo = Substitute.For<IGameRepository>();
        var villainRepo = Substitute.For<IVillainTreeRepository>();
        var unlocksRepo = Substitute.For<IBotUnlocksRepository>();

        var stitchNode = new VillainTreeNode
        {
            Id = Guid.NewGuid(),
            VillainId = "stitch",
            VillainName = "Stitch",
            UnlockedPieceId = null,
            DisplayOrder = 1,
            CreatedAtUtc = DateTime.UtcNow
        };

        var defeats = new[]
        {
            new BotUnlock
            {
                Id = Guid.NewGuid(),
                BotId = botId,
                VillainId = "stitch",
                UnlockedPieceId = null,
                DefeatedAtUtc = DateTime.UtcNow.AddHours(-1)
            }
        };

        villainRepo.GetNodeByVillainIdAsync("stitch", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<VillainTreeNode?>(stitchNode));
        unlocksRepo.GetDefeatedVillainsAsync(botId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(defeats.AsEnumerable()));

        var handler = BuildHandler(gameRepo, villainRepo, unlocksRepo);
        var command = new CreateSoloGameCommand(botId, "stitch");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: Should create without error
        Assert.NotEqual(Guid.Empty, result.GameId);
        Assert.Equal("stitch", result.VillainId);
    }

    [Fact]
    public async Task Handle_SavesGameToRepository()
    {
        // Arrange
        var botId = Guid.NewGuid();
        var gameRepo = Substitute.For<IGameRepository>();
        var villainRepo = Substitute.For<IVillainTreeRepository>();
        var unlocksRepo = Substitute.For<IBotUnlocksRepository>();

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
        unlocksRepo.GetDefeatedVillainsAsync(botId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Empty<BotUnlock>()));

        var handler = BuildHandler(gameRepo, villainRepo, unlocksRepo);
        var command = new CreateSoloGameCommand(botId, "stitch");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        await gameRepo.Received(1).SaveAsync(Arg.Any<Game>(), Arg.Any<CancellationToken>());
    }
}
