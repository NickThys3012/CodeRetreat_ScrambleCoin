using NSubstitute;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Ranking.GetAllBotsProgress;
using ScrambleCoin.Domain.Entities;

namespace ScrambleCoin.Application.Tests.Ranking;

/// <summary>
/// Unit tests for <see cref="GetAllBotsProgressQueryHandler"/> (Issue #57).
/// </summary>
public sealed class GetAllBotsProgressQueryHandlerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private readonly IBotUnlocksRepository _unlocksRepo =
        Substitute.For<IBotUnlocksRepository>();

    private readonly IRankingRepository _rankingRepo =
        Substitute.For<IRankingRepository>();

    private GetAllBotsProgressQueryHandler BuildHandler() =>
        new(_unlocksRepo, _rankingRepo);

    private static BotUnlock Unlock(Guid botId, string villainId, string? pieceId = null) =>
        new() { BotId = botId, VillainId = villainId, UnlockedPieceId = pieceId };

    private static RankingTrack Track(Guid botId, string botName) =>
        new(botId, botName, points: 0, wins: 0, draws: 0, losses: 0, gamesPlayed: 0, milestonesHit: []);

    // ── Core aggregation ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AggregatesUnlocksByBot_ReturnsCorrectVillainCountsAndPieceCounts()
    {
        // Arrange
        var botId = Guid.NewGuid();

        // Two distinct villains defeated; only one unlock has a piece reward.
        var unlocks = new List<BotUnlock>
        {
            Unlock(botId, "villain-a", "piece-mickey"),
            Unlock(botId, "villain-b") // no piece reward
        };

        _unlocksRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(unlocks.AsEnumerable());
        _rankingRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<RankingTrack>().AsReadOnly());

        // Act
        var result = await BuildHandler().Handle(new GetAllBotsProgressQuery(), CancellationToken.None);

        // Assert
        var dto = result.Single();
        Assert.Equal(botId, dto.BotId);
        Assert.Equal(2,     dto.VillainsDefeated);
        Assert.Equal(1,     dto.PiecesUnlocked);
    }

    [Fact]
    public async Task Handle_DeduplicatesVillains_WhenSameVillainDefeatedMultipleTimes()
    {
        // Arrange: same villain defeated twice (e.g. re-challenge).
        var botId = Guid.NewGuid();

        var unlocks = new List<BotUnlock>
        {
            Unlock(botId, "villain-a", "piece-one"),
            Unlock(botId, "villain-a", "piece-two") // duplicate villain
        };

        _unlocksRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(unlocks.AsEnumerable());
        _rankingRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<RankingTrack>().AsReadOnly());

        // Act
        var result = await BuildHandler().Handle(new GetAllBotsProgressQuery(), CancellationToken.None);

        // Assert: villain counted once even though two defeat records exist.
        var dto = result.Single();
        Assert.Equal(1, dto.VillainsDefeated);
        // Both unlock records have a piece, so PiecesUnlocked = 2.
        Assert.Equal(2, dto.PiecesUnlocked);
    }

    // ── Bot name resolution ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_UsesRankingTrackForBotName_WhenTrackExists()
    {
        // Arrange
        var botId = Guid.NewGuid();

        _unlocksRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { Unlock(botId, "villain-a") }.AsEnumerable());
        _rankingRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<RankingTrack> { Track(botId, "AlphaBot") }.AsReadOnly());

        // Act
        var result = await BuildHandler().Handle(new GetAllBotsProgressQuery(), CancellationToken.None);

        // Assert: display name from ranking track is used.
        Assert.Equal("AlphaBot", result.Single().BotName);
    }

    [Fact]
    public async Task Handle_FallsBackToShortId_WhenNoRankingTrack()
    {
        // Arrange
        var botId = Guid.NewGuid();

        _unlocksRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { Unlock(botId, "villain-a") }.AsEnumerable());
        _rankingRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<RankingTrack>().AsReadOnly());

        // Act
        var result = await BuildHandler().Handle(new GetAllBotsProgressQuery(), CancellationToken.None);

        // Assert: fallback is first-8-chars of GUID + ellipsis.
        var expectedFallback = botId.ToString()[..8] + "…";
        Assert.Equal(expectedFallback, result.Single().BotName);
    }

    // ── Ordering ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_OrdersByVillainsDefeatedDescending()
    {
        // Arrange
        var botA = Guid.NewGuid(); // 3 villains
        var botB = Guid.NewGuid(); // 1 villain
        var botC = Guid.NewGuid(); // 2 villains

        var unlocks = new List<BotUnlock>
        {
            Unlock(botA, "v1"), Unlock(botA, "v2"), Unlock(botA, "v3"),
            Unlock(botB, "v1"),
            Unlock(botC, "v1"), Unlock(botC, "v2")
        };

        _unlocksRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(unlocks.AsEnumerable());
        _rankingRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<RankingTrack>().AsReadOnly());

        // Act
        var result = await BuildHandler().Handle(new GetAllBotsProgressQuery(), CancellationToken.None);

        // Assert: sorted 3 → 2 → 1.
        Assert.Equal(3, result[0].VillainsDefeated);
        Assert.Equal(2, result[1].VillainsDefeated);
        Assert.Equal(1, result[2].VillainsDefeated);
    }

    [Fact]
    public async Task Handle_TiesInVillainsDefeated_OrdersByPiecesUnlockedDescending()
    {
        // Arrange: two bots with the same villain count but different piece counts.
        var botA = Guid.NewGuid(); // 1 villain, 2 pieces
        var botB = Guid.NewGuid(); // 1 villain, 1 piece

        var unlocks = new List<BotUnlock>
        {
            Unlock(botA, "v1", "piece-1"),
            Unlock(botA, "v1", "piece-2"), // duplicate villain but another piece
            Unlock(botB, "v1", "piece-x")
        };

        _unlocksRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(unlocks.AsEnumerable());
        _rankingRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<RankingTrack>().AsReadOnly());

        // Act
        var result = await BuildHandler().Handle(new GetAllBotsProgressQuery(), CancellationToken.None);

        // Assert: botA (2 pieces) comes before botB (1 piece).
        Assert.Equal(botA, result[0].BotId);
        Assert.Equal(botB, result[1].BotId);
    }

    // ── Empty state ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenNoUnlocks_ReturnsEmpty()
    {
        // Arrange
        _unlocksRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([]);
        _rankingRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<RankingTrack>().AsReadOnly());

        // Act
        var result = await BuildHandler().Handle(new GetAllBotsProgressQuery(), CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    // ── Multiple bots isolation ───────────────────────────────────────────────

    [Fact]
    public async Task Handle_DoesNotMixVillainsBetweenBots()
    {
        // Arrange: two bots that both defeated the same villain ID — each should count it separately.
        var botA = Guid.NewGuid();
        var botB = Guid.NewGuid();

        var unlocks = new List<BotUnlock>
        {
            Unlock(botA, "villain-shared"),
            Unlock(botB, "villain-shared")
        };

        _unlocksRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(unlocks.AsEnumerable());
        _rankingRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<RankingTrack>().AsReadOnly());

        // Act
        var result = await BuildHandler().Handle(new GetAllBotsProgressQuery(), CancellationToken.None);

        // Assert: each bot has exactly 1 villain, and results contain 2 entries.
        Assert.Equal(2, result.Count);
        Assert.All(result, dto => Assert.Equal(1, dto.VillainsDefeated));
    }
}
