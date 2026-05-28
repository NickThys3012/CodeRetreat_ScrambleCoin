using NSubstitute;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Ranking.GetLeaderboard;
using ScrambleCoin.Domain.Entities;

namespace ScrambleCoin.Application.Tests.Ranking;

/// <summary>
/// Unit tests for <see cref="GetLeaderboardQueryHandler"/> (Issue #53).
/// </summary>
public sealed class GetLeaderboardQueryHandlerTests
{
    private readonly IRankingRepository _repo = Substitute.For<IRankingRepository>();

    private GetLeaderboardQueryHandler BuildHandler() => new(_repo);

    // Helper to create a track with a known point value (via reconstitution ctor)
    private static RankingTrack TrackWithPoints(string name, int points, int wins = 0, int gamesPlayed = 0)
        => new(
            botId:        Guid.NewGuid(),
            botName:      name,
            points:       points,
            wins:         wins,
            draws:        0,
            losses:       0,
            gamesPlayed:  gamesPlayed,
            milestonesHit: []);

    // ── Sorting by points descending ──────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsBotsSortedByPointsDescending()
    {
        var low  = TrackWithPoints("LowBot",  5);
        var high = TrackWithPoints("HighBot", 20);
        var mid  = TrackWithPoints("MidBot",  10);

        _repo.GetAllAsync(Arg.Any<CancellationToken>())
             .Returns(new[] { low, mid, high }.ToList().AsReadOnly() as IReadOnlyList<RankingTrack>);

        var result = await BuildHandler().Handle(new GetLeaderboardQuery(), CancellationToken.None);

        Assert.Equal("HighBot", result[0].BotName);
        Assert.Equal("MidBot",  result[1].BotName);
        Assert.Equal("LowBot",  result[2].BotName);
    }

    [Fact]
    public async Task Handle_FirstEntryHasRankOne()
    {
        var trackA = TrackWithPoints("A", 30);
        var trackB = TrackWithPoints("B", 10);

        _repo.GetAllAsync(Arg.Any<CancellationToken>())
             .Returns(new[] { trackA, trackB }.ToList().AsReadOnly() as IReadOnlyList<RankingTrack>);

        var result = await BuildHandler().Handle(new GetLeaderboardQuery(), CancellationToken.None);

        Assert.Equal(1, result[0].Rank);
    }

    [Fact]
    public async Task Handle_RanksAreOneIndexed_InOrder()
    {
        var trackA = TrackWithPoints("A", 30);
        var trackB = TrackWithPoints("B", 20);
        var trackC = TrackWithPoints("C", 10);

        _repo.GetAllAsync(Arg.Any<CancellationToken>())
             .Returns(new[] { trackA, trackB, trackC }.ToList().AsReadOnly() as IReadOnlyList<RankingTrack>);

        var result = await BuildHandler().Handle(new GetLeaderboardQuery(), CancellationToken.None);

        Assert.Equal(1, result[0].Rank);
        Assert.Equal(2, result[1].Rank);
        Assert.Equal(3, result[2].Rank);
    }

    // ── Tie-break: points equal → wins descending ─────────────────────────────

    [Fact]
    public async Task Handle_TieInPoints_BotWithMoreWinsRanksHigher()
    {
        var moreWins  = TrackWithPoints("MoreWins",  10, wins: 5, gamesPlayed: 5);
        var fewerWins = TrackWithPoints("FewerWins", 10, wins: 2, gamesPlayed: 5);

        _repo.GetAllAsync(Arg.Any<CancellationToken>())
             .Returns(new[] { fewerWins, moreWins }.ToList().AsReadOnly() as IReadOnlyList<RankingTrack>);

        var result = await BuildHandler().Handle(new GetLeaderboardQuery(), CancellationToken.None);

        Assert.Equal("MoreWins", result[0].BotName);
    }

    [Fact]
    public async Task Handle_TieInPoints_TieInWins_FewerGamesPlayedRanksHigher()
    {
        // Same points & wins → fewer games played comes first
        var efficient = TrackWithPoints("Efficient",   10, wins: 3, gamesPlayed: 4);
        var less      = TrackWithPoints("LessEfficient", 10, wins: 3, gamesPlayed: 8);

        _repo.GetAllAsync(Arg.Any<CancellationToken>())
             .Returns(new[] { less, efficient }.ToList().AsReadOnly() as IReadOnlyList<RankingTrack>);

        var result = await BuildHandler().Handle(new GetLeaderboardQuery(), CancellationToken.None);

        Assert.Equal("Efficient", result[0].BotName);
    }

    // ── Empty leaderboard ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_EmptyLeaderboard_ReturnsEmptyList()
    {
        _repo.GetAllAsync(Arg.Any<CancellationToken>())
             .Returns(Array.Empty<RankingTrack>().ToList().AsReadOnly() as IReadOnlyList<RankingTrack>);

        var result = await BuildHandler().Handle(new GetLeaderboardQuery(), CancellationToken.None);

        Assert.Empty(result);
    }

    // ── DTO mapping ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_MapsAllFieldsToDto()
    {
        var botId = Guid.NewGuid();
        var track = new RankingTrack(
            botId:        botId,
            botName:      "DtoBot",
            points:       15,
            wins:         3,
            draws:        1,
            losses:       2,
            gamesPlayed:  6,
            milestonesHit: []);

        _repo.GetAllAsync(Arg.Any<CancellationToken>())
             .Returns(new[] { track }.ToList().AsReadOnly() as IReadOnlyList<RankingTrack>);

        var result = await BuildHandler().Handle(new GetLeaderboardQuery(), CancellationToken.None);

        var dto = result.Single();
        Assert.Equal(botId,    dto.BotId);
        Assert.Equal("DtoBot", dto.BotName);
        Assert.Equal(15,       dto.Points);
        Assert.Equal(3,        dto.Wins);
        Assert.Equal(1,        dto.Draws);
        Assert.Equal(2,        dto.Losses);
        Assert.Equal(6,        dto.GamesPlayed);
        Assert.Equal(1,        dto.Rank);
    }

    [Fact]
    public async Task Handle_SingleBot_HasRankOne()
    {
        var track = TrackWithPoints("OnlyBot", 10);

        _repo.GetAllAsync(Arg.Any<CancellationToken>())
             .Returns(new[] { track }.ToList().AsReadOnly() as IReadOnlyList<RankingTrack>);

        var result = await BuildHandler().Handle(new GetLeaderboardQuery(), CancellationToken.None);

        Assert.Equal(1, result.Single().Rank);
    }
}
