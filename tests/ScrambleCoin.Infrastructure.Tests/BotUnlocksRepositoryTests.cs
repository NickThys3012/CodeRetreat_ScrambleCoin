using Microsoft.EntityFrameworkCore;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Infrastructure.Persistence;

namespace ScrambleCoin.Infrastructure.Tests;

/// <summary>
/// Integration tests for <see cref="BotUnlocksRepository"/> (Issue #42).
/// Tests against an in-memory SQLite database.
/// </summary>
public class BotUnlocksRepositoryTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static DbContextOptions<ScrambleCoinDbContext> BuildInMemoryOptions(string dbName) =>
        new DbContextOptionsBuilder<ScrambleCoinDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDefeatedVillainsAsync_ReturnsAllDefeatsForBot()
    {
        // Arrange
        var botId = Guid.NewGuid();
        var options = BuildInMemoryOptions(nameof(GetDefeatedVillainsAsync_ReturnsAllDefeatsForBot));

        var unlocks = new[]
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
                VillainId = "jafar",
                UnlockedPieceId = "Goofy",
                DefeatedAtUtc = DateTime.UtcNow
            }
        };

        await using (var context = new ScrambleCoinDbContext(options))
        {
            context.BotUnlocks.AddRange(unlocks);
            await context.SaveChangesAsync();
        }

        // Act
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var repository = new BotUnlocksRepository(context);
            var result = await repository.GetDefeatedVillainsAsync(botId);

            // Assert
            Assert.NotNull(result);
            var resultList = result.ToList();
            Assert.Equal(2, resultList.Count);
            Assert.Contains(resultList, u => u.VillainId == "stitch");
            Assert.Contains(resultList, u => u.VillainId == "jafar");
        }
    }

    [Fact]
    public async Task GetDefeatedVillainsAsync_ReturnsEmptyForBotWithNoDefeats()
    {
        // Arrange
        var botId = Guid.NewGuid();
        var options = BuildInMemoryOptions(nameof(GetDefeatedVillainsAsync_ReturnsEmptyForBotWithNoDefeats));

        // Act
        await using var context = new ScrambleCoinDbContext(options);
        var repository = new BotUnlocksRepository(context);
        var result = await repository.GetDefeatedVillainsAsync(botId);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetDefeatedVillainsAsync_OnlyReturnsBotSpecificDefeats()
    {
        // Arrange
        var botId1 = Guid.NewGuid();
        var botId2 = Guid.NewGuid();
        var options = BuildInMemoryOptions(nameof(GetDefeatedVillainsAsync_OnlyReturnsBotSpecificDefeats));

        var unlocks = new[]
        {
            new BotUnlock
            {
                Id = Guid.NewGuid(),
                BotId = botId1,
                VillainId = "stitch",
                UnlockedPieceId = null,
                DefeatedAtUtc = DateTime.UtcNow
            },
            new BotUnlock
            {
                Id = Guid.NewGuid(),
                BotId = botId2,
                VillainId = "jafar",
                UnlockedPieceId = "Goofy",
                DefeatedAtUtc = DateTime.UtcNow
            }
        };

        await using (var context = new ScrambleCoinDbContext(options))
        {
            context.BotUnlocks.AddRange(unlocks);
            await context.SaveChangesAsync();
        }

        // Act
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var repository = new BotUnlocksRepository(context);
            var result = await repository.GetDefeatedVillainsAsync(botId1);

            // Assert
            var resultList = result.ToList();
            Assert.Single(resultList);
            Assert.Equal("stitch", resultList[0].VillainId);
            Assert.Equal(botId1, resultList[0].BotId);
        }
    }

    [Fact]
    public async Task RecordDefeatAsync_InsertsNewDefeat()
    {
        // Arrange
        var botId = Guid.NewGuid();
        var options = BuildInMemoryOptions(nameof(RecordDefeatAsync_InsertsNewDefeat));

        // Act
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var repository = new BotUnlocksRepository(context);
            await repository.RecordDefeatAsync(botId, "stitch", null);
        }

        // Assert
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var recorded = await context.BotUnlocks.FirstOrDefaultAsync(u => u.BotId == botId && u.VillainId == "stitch");
            Assert.NotNull(recorded);
            Assert.Equal("stitch", recorded.VillainId);
            Assert.Null(recorded.UnlockedPieceId);
        }
    }

    [Fact]
    public async Task RecordDefeatAsync_WithPiece_RecordsUnlock()
    {
        // Arrange
        var botId = Guid.NewGuid();
        var options = BuildInMemoryOptions(nameof(RecordDefeatAsync_WithPiece_RecordsUnlock));

        // Act
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var repository = new BotUnlocksRepository(context);
            await repository.RecordDefeatAsync(botId, "jafar", "Goofy");
        }

        // Assert
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var recorded = await context.BotUnlocks.FirstOrDefaultAsync(u => u.BotId == botId && u.VillainId == "jafar");
            Assert.NotNull(recorded);
            Assert.Equal("Goofy", recorded.UnlockedPieceId);
        }
    }

    [Fact]
    public async Task RecordDefeatAsync_ReChallengeUpdatesDefeatedTime()
    {
        // Arrange
        var botId = Guid.NewGuid();
        var options = BuildInMemoryOptions(nameof(RecordDefeatAsync_ReChallengeUpdatesDefeatedTime));

        var originalTime = DateTime.UtcNow.AddHours(-1);

        await using (var context = new ScrambleCoinDbContext(options))
        {
            var unlock = new BotUnlock
            {
                Id = Guid.NewGuid(),
                BotId = botId,
                VillainId = "stitch",
                UnlockedPieceId = null,
                DefeatedAtUtc = originalTime
            };
            context.BotUnlocks.Add(unlock);
            await context.SaveChangesAsync();
        }

        // Act: Re-challenge the villain
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var repository = new BotUnlocksRepository(context);
            await repository.RecordDefeatAsync(botId, "stitch", null);
        }

        // Assert: DefeatedAtUtc should be updated
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var recorded = await context.BotUnlocks.FirstOrDefaultAsync(u => u.BotId == botId && u.VillainId == "stitch");
            Assert.NotNull(recorded);
            Assert.True(recorded.DefeatedAtUtc > originalTime);
        }
    }

    [Fact]
    public async Task RecordDefeatAsync_ReChallengeKeepsExistingPiece()
    {
        // Arrange
        var botId = Guid.NewGuid();
        var options = BuildInMemoryOptions(nameof(RecordDefeatAsync_ReChallengeKeepsExistingPiece));

        await using (var context = new ScrambleCoinDbContext(options))
        {
            var unlock = new BotUnlock
            {
                Id = Guid.NewGuid(),
                BotId = botId,
                VillainId = "jafar",
                UnlockedPieceId = "Goofy",
                DefeatedAtUtc = DateTime.UtcNow.AddHours(-1)
            };
            context.BotUnlocks.Add(unlock);
            await context.SaveChangesAsync();
        }

        // Act: Re-challenge and don't provide a piece
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var repository = new BotUnlocksRepository(context);
            await repository.RecordDefeatAsync(botId, "jafar", null);
        }

        // Assert: UnlockedPieceId should remain "Goofy"
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var recorded = await context.BotUnlocks.FirstOrDefaultAsync(u => u.BotId == botId && u.VillainId == "jafar");
            Assert.NotNull(recorded);
            Assert.Equal("Goofy", recorded.UnlockedPieceId); // Original piece preserved
        }
    }

    [Fact]
    public async Task GetUnlockedPieceIdsAsync_ReturnsOnlyPiecesWithValues()
    {
        // Arrange
        var botId = Guid.NewGuid();
        var options = BuildInMemoryOptions(nameof(GetUnlockedPieceIdsAsync_ReturnsOnlyPiecesWithValues));

        var unlocks = new[]
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
                VillainId = "jafar",
                UnlockedPieceId = "Goofy",
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

        await using (var context = new ScrambleCoinDbContext(options))
        {
            context.BotUnlocks.AddRange(unlocks);
            await context.SaveChangesAsync();
        }

        // Act
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var repository = new BotUnlocksRepository(context);
            var result = await repository.GetUnlockedPieceIdsAsync(botId);

            // Assert: Only pieces with non-null values
            var resultList = result.ToList();
            Assert.Equal(2, resultList.Count);
            Assert.Contains("Goofy", resultList);
            Assert.Contains("Merlin", resultList);
        }
    }

    [Fact]
    public async Task HasDefeatedVillainAsync_ReturnsTrueWhenDefeated()
    {
        // Arrange
        var botId = Guid.NewGuid();
        var options = BuildInMemoryOptions(nameof(HasDefeatedVillainAsync_ReturnsTrueWhenDefeated));

        await using (var context = new ScrambleCoinDbContext(options))
        {
            var unlock = new BotUnlock
            {
                Id = Guid.NewGuid(),
                BotId = botId,
                VillainId = "stitch",
                UnlockedPieceId = null,
                DefeatedAtUtc = DateTime.UtcNow
            };
            context.BotUnlocks.Add(unlock);
            await context.SaveChangesAsync();
        }

        // Act
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var repository = new BotUnlocksRepository(context);
            var result = await repository.HasDefeatedVillainAsync(botId, "stitch");

            // Assert
            Assert.True(result);
        }
    }

    [Fact]
    public async Task HasDefeatedVillainAsync_ReturnsFalseWhenNotDefeated()
    {
        // Arrange
        var botId = Guid.NewGuid();
        var options = BuildInMemoryOptions(nameof(HasDefeatedVillainAsync_ReturnsFalseWhenNotDefeated));

        // Act
        await using var context = new ScrambleCoinDbContext(options);
        var repository = new BotUnlocksRepository(context);
        var result = await repository.HasDefeatedVillainAsync(botId, "nonexistent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task RecordDefeatAsync_AllowsMultipleDefeatsPerBotVillainPair()
    {
        // Arrange
        var botId = Guid.NewGuid();
        var options = BuildInMemoryOptions(nameof(RecordDefeatAsync_AllowsMultipleDefeatsPerBotVillainPair));

        // Act: First defeat
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var repository = new BotUnlocksRepository(context);
            await repository.RecordDefeatAsync(botId, "stitch", null);
        }

        // Second defeat (re-challenge)
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var repository = new BotUnlocksRepository(context);
            await repository.RecordDefeatAsync(botId, "stitch", null);
        }

        // Assert
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var recorded = await context.BotUnlocks.Where(u => u.BotId == botId && u.VillainId == "stitch").ToListAsync();
            // UPSERT means we should have only 1 record (updated, not duplicated)
            Assert.Single(recorded);
        }
    }

    [Fact]
    public async Task RecordDefeatAsync_PreservesPieceOnSecondDefeat()
    {
        // Arrange
        var botId = Guid.NewGuid();
        var options = BuildInMemoryOptions(nameof(RecordDefeatAsync_PreservesPieceOnSecondDefeat));

        // Act: First defeat with a piece
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var repository = new BotUnlocksRepository(context);
            await repository.RecordDefeatAsync(botId, "jafar", "Goofy");
        }

        // Second defeat without a piece specified
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var repository = new BotUnlocksRepository(context);
            await repository.RecordDefeatAsync(botId, "jafar", null);
        }

        // Assert: Piece should still be "Goofy"
        await using (var context = new ScrambleCoinDbContext(options))
        {
            var recorded = await context.BotUnlocks.FirstOrDefaultAsync(u => u.BotId == botId && u.VillainId == "jafar");
            Assert.NotNull(recorded);
            Assert.Equal("Goofy", recorded.UnlockedPieceId);
        }
    }
}
