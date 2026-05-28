using Microsoft.EntityFrameworkCore;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Infrastructure.Persistence;

namespace ScrambleCoin.Infrastructure.Tests.Persistence;

/// <summary>
/// Integration tests for <see cref="RankingRepository"/> using EF Core InMemory.
/// Each test uses an isolated database instance to prevent state leakage between tests.
/// </summary>
public sealed class RankingRepositoryTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static DbContextOptions<ScrambleCoinDbContext> BuildOptions(string dbName) =>
        new DbContextOptionsBuilder<ScrambleCoinDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

    /// <summary>Creates a fresh <see cref="RankingTrack"/> with zero stats.</summary>
    private static RankingTrack NewTrack(string name = "TestBot") =>
        new(Guid.NewGuid(), name);

    /// <summary>
    /// Creates a <see cref="RankingTrack"/> reconstituted from known data
    /// (bypasses the zero-init constructor).
    /// </summary>
    private static RankingTrack ReconstitutedTrack(
        Guid botId,
        string name,
        int points,
        int wins,
        int draws,
        int losses,
        int gamesPlayed,
        IEnumerable<int>? milestonesHit = null) =>
        new(botId, name, points, wins, draws, losses, gamesPlayed, milestonesHit ?? []);

    // ── SaveAsync — new track ─────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_NewTrack_PersistsToDatabase()
    {
        var options = BuildOptions(nameof(SaveAsync_NewTrack_PersistsToDatabase));
        var track = NewTrack("PersistBot");

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var repo = new RankingRepository(ctx);
            await repo.SaveAsync(track);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new RankingRepository(ctx).GetByBotIdAsync(track.BotId);
            Assert.NotNull(loaded);
        }
    }

    [Fact]
    public async Task SaveAsync_NewTrack_PreservesBotId()
    {
        var options = BuildOptions(nameof(SaveAsync_NewTrack_PreservesBotId));
        var track = NewTrack();

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new RankingRepository(ctx).SaveAsync(track);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new RankingRepository(ctx).GetByBotIdAsync(track.BotId);
            Assert.Equal(track.BotId, loaded!.BotId);
        }
    }

    [Fact]
    public async Task SaveAsync_NewTrack_PreservesBotName()
    {
        var options = BuildOptions(nameof(SaveAsync_NewTrack_PreservesBotName));
        var track = NewTrack("NamedBot");

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new RankingRepository(ctx).SaveAsync(track);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new RankingRepository(ctx).GetByBotIdAsync(track.BotId);
            Assert.Equal("NamedBot", loaded!.BotName);
        }
    }

    [Fact]
    public async Task SaveAsync_NewTrack_PreservesPoints()
    {
        var options = BuildOptions(nameof(SaveAsync_NewTrack_PreservesPoints));
        var botId = Guid.NewGuid();
        var track = ReconstitutedTrack(botId, "Bot", points: 12, wins: 4, draws: 0, losses: 0, gamesPlayed: 4);

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new RankingRepository(ctx).SaveAsync(track);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new RankingRepository(ctx).GetByBotIdAsync(botId);
            Assert.Equal(12, loaded!.Points);
        }
    }

    [Fact]
    public async Task SaveAsync_NewTrack_PreservesWinsDrawsLosses()
    {
        var options = BuildOptions(nameof(SaveAsync_NewTrack_PreservesWinsDrawsLosses));
        var botId = Guid.NewGuid();
        var track = ReconstitutedTrack(botId, "Bot", points: 8, wins: 2, draws: 1, losses: 3, gamesPlayed: 6);

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new RankingRepository(ctx).SaveAsync(track);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new RankingRepository(ctx).GetByBotIdAsync(botId);
            Assert.Equal(2, loaded!.Wins);
            Assert.Equal(1, loaded.Draws);
            Assert.Equal(3, loaded.Losses);
        }
    }

    [Fact]
    public async Task SaveAsync_NewTrack_PreservesGamesPlayed()
    {
        var options = BuildOptions(nameof(SaveAsync_NewTrack_PreservesGamesPlayed));
        var botId = Guid.NewGuid();
        var track = ReconstitutedTrack(botId, "Bot", points: 6, wins: 2, draws: 0, losses: 4, gamesPlayed: 6);

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new RankingRepository(ctx).SaveAsync(track);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new RankingRepository(ctx).GetByBotIdAsync(botId);
            Assert.Equal(6, loaded!.GamesPlayed);
        }
    }

    [Fact]
    public async Task SaveAsync_NewTrack_PreservesMilestonesHit()
    {
        var options = BuildOptions(nameof(SaveAsync_NewTrack_PreservesMilestonesHit));
        var botId = Guid.NewGuid();
        var milestones = new[] { 3, 9, 15 };
        var track = ReconstitutedTrack(botId, "Bot", points: 15, wins: 5, draws: 0, losses: 0, gamesPlayed: 5,
            milestonesHit: milestones);

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new RankingRepository(ctx).SaveAsync(track);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new RankingRepository(ctx).GetByBotIdAsync(botId);
            Assert.Equal(milestones.OrderBy(x => x), loaded!.MilestonesHit.OrderBy(x => x));
        }
    }

    // ── SaveAsync — update existing track ─────────────────────────────────────

    [Fact]
    public async Task SaveAsync_ExistingTrack_UpdatesInDatabase()
    {
        var options = BuildOptions(nameof(SaveAsync_ExistingTrack_UpdatesInDatabase));
        var track = NewTrack("UpdateBot");

        // First save
        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new RankingRepository(ctx).SaveAsync(track);
            await ctx.SaveChangesAsync();
        }

        // Mutate and save again
        track.RecordWin();
        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new RankingRepository(ctx).SaveAsync(track);
            await ctx.SaveChangesAsync();
        }

        // Verify update was persisted
        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new RankingRepository(ctx).GetByBotIdAsync(track.BotId);
            Assert.Equal(3, loaded!.Points);
            Assert.Equal(1, loaded.Wins);
        }
    }

    [Fact]
    public async Task SaveAsync_ExistingTrack_UpdatesGamesPlayed()
    {
        var options = BuildOptions(nameof(SaveAsync_ExistingTrack_UpdatesGamesPlayed));
        var track = NewTrack();

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new RankingRepository(ctx).SaveAsync(track);
            await ctx.SaveChangesAsync();
        }

        track.RecordDraw();
        track.RecordLoss();

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new RankingRepository(ctx).SaveAsync(track);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new RankingRepository(ctx).GetByBotIdAsync(track.BotId);
            Assert.Equal(2, loaded!.GamesPlayed);
        }
    }

    // ── GetByBotIdAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetByBotIdAsync_ExistingBot_ReturnsTrack()
    {
        var options = BuildOptions(nameof(GetByBotIdAsync_ExistingBot_ReturnsTrack));
        var track = NewTrack("ExistingBot");

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new RankingRepository(ctx).SaveAsync(track);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new RankingRepository(ctx).GetByBotIdAsync(track.BotId);
            Assert.NotNull(loaded);
            Assert.Equal(track.BotId, loaded!.BotId);
        }
    }

    [Fact]
    public async Task GetByBotIdAsync_ExistingBot_ReturnsBotName()
    {
        var options = BuildOptions(nameof(GetByBotIdAsync_ExistingBot_ReturnsBotName));
        var track = NewTrack("NameCheckBot");

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new RankingRepository(ctx).SaveAsync(track);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new RankingRepository(ctx).GetByBotIdAsync(track.BotId);
            Assert.Equal("NameCheckBot", loaded!.BotName);
        }
    }

    [Fact]
    public async Task GetByBotIdAsync_UnknownBot_ReturnsNull()
    {
        var options = BuildOptions(nameof(GetByBotIdAsync_UnknownBot_ReturnsNull));

        await using var ctx = new ScrambleCoinDbContext(options);
        var result = await new RankingRepository(ctx).GetByBotIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    // ── GetAllAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_MultipleTracksExist_ReturnsAll()
    {
        var options = BuildOptions(nameof(GetAllAsync_MultipleTracksExist_ReturnsAll));
        var trackA = NewTrack("BotA");
        var trackB = NewTrack("BotB");
        var trackC = NewTrack("BotC");

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var repo = new RankingRepository(ctx);
            await repo.SaveAsync(trackA);
            await repo.SaveAsync(trackB);
            await repo.SaveAsync(trackC);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var all = await new RankingRepository(ctx).GetAllAsync();
            Assert.Equal(3, all.Count);
        }
    }

    [Fact]
    public async Task GetAllAsync_MultipleTracksExist_ContainsAllBotIds()
    {
        var options = BuildOptions(nameof(GetAllAsync_MultipleTracksExist_ContainsAllBotIds));
        var trackA = NewTrack("BotA");
        var trackB = NewTrack("BotB");

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var repo = new RankingRepository(ctx);
            await repo.SaveAsync(trackA);
            await repo.SaveAsync(trackB);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var all = await new RankingRepository(ctx).GetAllAsync();
            var ids = all.Select(t => t.BotId).ToHashSet();
            Assert.Contains(trackA.BotId, ids);
            Assert.Contains(trackB.BotId, ids);
        }
    }

    [Fact]
    public async Task GetAllAsync_Empty_ReturnsEmptyList()
    {
        var options = BuildOptions(nameof(GetAllAsync_Empty_ReturnsEmptyList));

        await using var ctx = new ScrambleCoinDbContext(options);
        var all = await new RankingRepository(ctx).GetAllAsync();

        Assert.Empty(all);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsTracksWithCorrectPoints()
    {
        var options = BuildOptions(nameof(GetAllAsync_ReturnsTracksWithCorrectPoints));
        var botId = Guid.NewGuid();
        var track = ReconstitutedTrack(botId, "PointsBot", points: 21, wins: 7, draws: 0, losses: 0, gamesPlayed: 7);

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new RankingRepository(ctx).SaveAsync(track);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var all = await new RankingRepository(ctx).GetAllAsync();
            Assert.Equal(21, all.Single().Points);
        }
    }

    // ── Milestones round-trip ─────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_TrackWithNoMilestones_RoundTripsEmptyList()
    {
        var options = BuildOptions(nameof(SaveAsync_TrackWithNoMilestones_RoundTripsEmptyList));
        var track = NewTrack();

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new RankingRepository(ctx).SaveAsync(track);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new RankingRepository(ctx).GetByBotIdAsync(track.BotId);
            Assert.Empty(loaded!.MilestonesHit);
        }
    }

    [Fact]
    public async Task SaveAsync_TrackWithMilestones_RoundTripsAllMilestones()
    {
        var options = BuildOptions(nameof(SaveAsync_TrackWithMilestones_RoundTripsAllMilestones));
        var botId = Guid.NewGuid();
        var expectedMilestones = new[] { 3, 9, 15, 24 };
        var track = ReconstitutedTrack(botId, "MilestoneBot",
            points: 24, wins: 8, draws: 0, losses: 0, gamesPlayed: 8,
            milestonesHit: expectedMilestones);

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new RankingRepository(ctx).SaveAsync(track);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new RankingRepository(ctx).GetByBotIdAsync(botId);
            Assert.Equal(
                expectedMilestones.OrderBy(x => x),
                loaded!.MilestonesHit.OrderBy(x => x));
        }
    }
}
