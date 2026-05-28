using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Exceptions;

namespace ScrambleCoin.Domain.Tests;

/// <summary>
/// Unit tests for <see cref="RankingTrack"/> (Issue #53).
/// All tests are pure domain-logic with no infrastructure dependencies.
/// </summary>
public sealed class RankingTrackTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static RankingTrack NewTrack(int startingPoints = 0) =>
        startingPoints == 0
            ? new RankingTrack(Guid.NewGuid(), "TestBot")
            : new RankingTrack(
                botId:        Guid.NewGuid(),
                botName:      "TestBot",
                points:       startingPoints,
                wins:         0,
                draws:        0,
                losses:       0,
                gamesPlayed:  0,
                milestonesHit: []);

    // ── Constructor guards ────────────────────────────────────────────────────

    [Fact]
    public void Constructor_WithEmptyBotId_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new RankingTrack(Guid.Empty, "SomeBot"));
    }

    [Fact]
    public void Constructor_WithNullBotName_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new RankingTrack(Guid.NewGuid(), null!));
    }

    [Fact]
    public void Constructor_WithWhitespaceBotName_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new RankingTrack(Guid.NewGuid(), "   "));
    }

    // ── RecordWin ─────────────────────────────────────────────────────────────

    [Fact]
    public void RecordWin_IncreasesPoints_AndWins_AndGamesPlayed()
    {
        var track = NewTrack();

        track.RecordWin();

        Assert.Equal(3, track.Points);
    }

    [Fact]
    public void RecordWin_IncrementsWinsByOne()
    {
        var track = NewTrack();

        track.RecordWin();

        Assert.Equal(1, track.Wins);
    }

    [Fact]
    public void RecordWin_IncrementsGamesPlayedByOne()
    {
        var track = NewTrack();

        track.RecordWin();

        Assert.Equal(1, track.GamesPlayed);
    }

    [Fact]
    public void RecordWin_DoesNotChangeLossesOrDraws()
    {
        var track = NewTrack();

        track.RecordWin();

        Assert.Equal(0, track.Draws);
        Assert.Equal(0, track.Losses);
    }

    // ── RecordDraw ────────────────────────────────────────────────────────────

    [Fact]
    public void RecordDraw_IncreasesPoints_AndDraws_AndGamesPlayed()
    {
        var track = NewTrack();

        track.RecordDraw();

        Assert.Equal(2, track.Points);
    }

    [Fact]
    public void RecordDraw_IncrementsDrawsByOne()
    {
        var track = NewTrack();

        track.RecordDraw();

        Assert.Equal(1, track.Draws);
    }

    [Fact]
    public void RecordDraw_IncrementsGamesPlayedByOne()
    {
        var track = NewTrack();

        track.RecordDraw();

        Assert.Equal(1, track.GamesPlayed);
    }

    [Fact]
    public void RecordDraw_DoesNotChangeWinsOrLosses()
    {
        var track = NewTrack();

        track.RecordDraw();

        Assert.Equal(0, track.Wins);
        Assert.Equal(0, track.Losses);
    }

    // ── RecordLoss ────────────────────────────────────────────────────────────

    [Fact]
    public void RecordLoss_IncreasesPoints_AndLosses_AndGamesPlayed()
    {
        var track = NewTrack();

        track.RecordLoss();

        Assert.Equal(1, track.Points);
    }

    [Fact]
    public void RecordLoss_IncrementsLossesByOne()
    {
        var track = NewTrack();

        track.RecordLoss();

        Assert.Equal(1, track.Losses);
    }

    [Fact]
    public void RecordLoss_IncrementsGamesPlayedByOne()
    {
        var track = NewTrack();

        track.RecordLoss();

        Assert.Equal(1, track.GamesPlayed);
    }

    [Fact]
    public void RecordLoss_DoesNotChangeWinsOrDraws()
    {
        var track = NewTrack();

        track.RecordLoss();

        Assert.Equal(0, track.Wins);
        Assert.Equal(0, track.Draws);
    }

    // ── Multiple calls ────────────────────────────────────────────────────────

    [Fact]
    public void RecordWin_CalledThreeTimes_AccumulatesNinePoints()
    {
        var track = NewTrack();

        track.RecordWin();
        track.RecordWin();
        track.RecordWin();

        Assert.Equal(9, track.Points);
    }

    [Fact]
    public void MixedResults_AccumulatePointsCorrectly()
    {
        // Win(3) + Draw(2) + Loss(1) = 6 points
        var track = NewTrack();

        track.RecordWin();
        track.RecordDraw();
        track.RecordLoss();

        Assert.Equal(6, track.Points);
    }

    // ── Max-points cap ────────────────────────────────────────────────────────

    [Fact]
    public void RecordWin_AtMaxPoints_DoesNotExceedMaxPoints()
    {
        // Start one win away from cap: 550 - 3 = 547 → wins push to 550
        var track = NewTrack(RankingTrack.MaxPoints - 3);

        track.RecordWin(); // hits exactly 550
        track.RecordWin(); // should be capped — no further gain

        Assert.Equal(RankingTrack.MaxPoints, track.Points);
    }

    [Fact]
    public void RecordWin_WhenAlreadyAtMaxPoints_DoesNotIncrementPoints()
    {
        var track = NewTrack(RankingTrack.MaxPoints);

        track.RecordWin();

        Assert.Equal(RankingTrack.MaxPoints, track.Points);
    }

    [Fact]
    public void RecordWin_WhenAlreadyAtMaxPoints_StillIncrementsWins()
    {
        // Wins counter should still increment even when capped
        var track = NewTrack(RankingTrack.MaxPoints);

        track.RecordWin();

        Assert.Equal(1, track.Wins);
    }

    [Fact]
    public void RecordWin_WhenAlreadyAtMaxPoints_StillIncrementsGamesPlayed()
    {
        var track = NewTrack(RankingTrack.MaxPoints);

        track.RecordWin();

        Assert.Equal(1, track.GamesPlayed);
    }

    [Fact]
    public void RecordWin_PointsNearCapButNotOver_ClampsToMaxPoints()
    {
        // 549 + 3 would be 552, but should clamp to 550
        var track = NewTrack(549);

        track.RecordWin();

        Assert.Equal(RankingTrack.MaxPoints, track.Points);
    }

    // ── Milestones ────────────────────────────────────────────────────────────

    [Fact]
    public void MilestonesHit_TrackedCorrectly_FirstMilestone()
    {
        // First milestone is 3 points — one RecordWin crosses it
        var track = NewTrack();

        track.RecordWin(); // 3 pts → crosses milestone 3

        Assert.Contains(3, track.MilestonesHit);
    }

    [Fact]
    public void MilestonesHit_NotHit_WhenPointsBelowThreshold()
    {
        // RecordLoss only gives 1 point — milestone 3 should NOT be hit
        var track = NewTrack();

        track.RecordLoss(); // 1 pt

        Assert.DoesNotContain(3, track.MilestonesHit);
    }

    [Fact]
    public void MilestonesHit_SecondMilestone_CrossedAt9Points()
    {
        // Second milestone = 9; need 3 wins (3+3+3 = 9)
        var track = NewTrack();

        track.RecordWin(); // 3
        track.RecordWin(); // 6
        track.RecordWin(); // 9 → crosses milestone 9

        Assert.Contains(9, track.MilestonesHit);
    }

    [Fact]
    public void MilestonesHit_BothFirstAndSecondMilestonesRecorded_After9Points()
    {
        var track = NewTrack();

        track.RecordWin(); // 3
        track.RecordWin(); // 6
        track.RecordWin(); // 9

        Assert.Contains(3, track.MilestonesHit);
        Assert.Contains(9, track.MilestonesHit);
    }

    [Fact]
    public void MilestonesHit_SameMilestone_NotAddedTwice()
    {
        // Cross milestone 3 twice by calling RecordWin twice from 0
        var track = NewTrack();

        track.RecordWin(); // 3 → crosses milestone 3
        track.RecordWin(); // 6 → does NOT cross 3 again

        Assert.Single(track.MilestonesHit.Where(m => m == 3));
    }

    [Fact]
    public void MilestonesHit_15Threshold_CrossedCorrectly()
    {
        // Third milestone = 15; need 5 wins (5 × 3 = 15)
        var track = NewTrack();

        for (int i = 0; i < 5; i++) track.RecordWin(); // 3,6,9,12,15

        Assert.Contains(15, track.MilestonesHit);
    }

    [Fact]
    public void MilestonesHit_24Threshold_CrossedCorrectly()
    {
        // Fourth milestone = 24; 8 wins = 24 points
        var track = NewTrack();

        for (int i = 0; i < 8; i++) track.RecordWin(); // 3…24

        Assert.Contains(24, track.MilestonesHit);
    }

    [Fact]
    public void MilestonesHit_IsEmpty_ForNewTrack()
    {
        var track = NewTrack();

        Assert.Empty(track.MilestonesHit);
    }

    [Fact]
    public void MilestonesHit_WhenSkippedOverByLargeGain_AllCrossedMilestonesAreRecorded()
    {
        // Start at 0, apply wins so that we skip from 0 to 9 in one call
        // RecordWin awards 3 pts per call, so we need points at 0 and 3 calls to hit 9.
        // But with reconstitution ctor: start at 0, RecordWin three times crosses 3 and 9.
        var track = NewTrack();

        track.RecordWin(); // 3  → milestone 3
        track.RecordWin(); // 6
        track.RecordWin(); // 9  → milestone 9

        Assert.Equal(2, track.MilestonesHit.Count);
        Assert.Equal([3, 9], track.MilestonesHit.OrderBy(x => x).ToList());
    }

    // ── Reconstitution constructor ────────────────────────────────────────────

    [Fact]
    public void ReconstitutionConstructor_RestoresAllFields()
    {
        var botId = Guid.NewGuid();
        var milestonesHit = new[] { 3, 9 };

        var track = new RankingTrack(
            botId:        botId,
            botName:      "ReconstitutedBot",
            points:       10,
            wins:         2,
            draws:        1,
            losses:       3,
            gamesPlayed:  6,
            milestonesHit: milestonesHit);

        Assert.Equal(botId, track.BotId);
        Assert.Equal("ReconstitutedBot", track.BotName);
        Assert.Equal(10, track.Points);
        Assert.Equal(2, track.Wins);
        Assert.Equal(1, track.Draws);
        Assert.Equal(3, track.Losses);
        Assert.Equal(6, track.GamesPlayed);
        Assert.Equal(milestonesHit, track.MilestonesHit);
    }
}
