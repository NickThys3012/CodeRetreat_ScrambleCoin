using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.Tournaments;

namespace ScrambleCoin.Domain.Tests;

/// <summary>
/// Unit tests for the <see cref="Tournament"/> aggregate (Issue #52).
/// Covers round-robin scheduling, standings computation, knockout bracket generation,
/// bye propagation, result recording, and lifecycle transitions.
/// </summary>
public class TournamentTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static readonly IReadOnlyList<string> DefaultLineup =
        ["Mickey", "Minnie", "Donald", "Goofy", "Scrooge"];

    /// <summary>Creates a tournament and optionally populates it with participants.</summary>
    private static Tournament NewTournament(int participantCount, int topN = 4, int maxParticipants = 16)
    {
        var t = new Tournament(
            Guid.NewGuid(),
            "Test Cup",
            Math.Max(participantCount, maxParticipants),
            topN,
            DateTimeOffset.UtcNow);

        for (int i = 0; i < participantCount; i++)
            t.AddParticipant(Guid.NewGuid(), $"Bot{i}", DefaultLineup);

        return t;
    }

    /// <summary>
    /// Completes all group-stage matches in the tournament by recording BotOne as the winner
    /// with coin scores 10–5.
    /// </summary>
    private static void CompleteAllGroupMatches(Tournament t)
    {
        foreach (var match in t.GroupMatches)
            match.RecordResult(match.BotOne, false, 10, 5);
    }

    // ── Canonical ordering helpers ─────────────────────────────────────────────

    private static Guid Min(Guid a, Guid b) => a.CompareTo(b) <= 0 ? a : b;
    private static Guid Max(Guid a, Guid b) => a.CompareTo(b) >= 0 ? a : b;

    // ══════════════════════════════════════════════════════════════════════════
    // Constructor
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Constructor_DefaultStatus_IsPending()
    {
        var t = new Tournament(Guid.NewGuid(), "Cup", 4, 2, DateTimeOffset.UtcNow);
        Assert.Equal(TournamentStatus.Pending, t.Status);
    }

    [Fact]
    public void Constructor_EmptyName_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() =>
            new Tournament(Guid.NewGuid(), "", 4, 2, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Constructor_MaxParticipantsLessThanTwo_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() =>
            new Tournament(Guid.NewGuid(), "Cup", 1, 2, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Constructor_TopNLessThanTwo_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() =>
            new Tournament(Guid.NewGuid(), "Cup", 4, 1, DateTimeOffset.UtcNow));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AddParticipant
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AddParticipant_DuplicateBot_ThrowsDomainException()
    {
        var t = NewTournament(0);
        var botId = Guid.NewGuid();
        t.AddParticipant(botId, "Bot", DefaultLineup);

        Assert.Throws<DomainException>(() =>
            t.AddParticipant(botId, "Bot", DefaultLineup));
    }

    [Fact]
    public void AddParticipant_WhenNotPending_ThrowsTournamentInvalidStateException()
    {
        var t = NewTournament(2);
        t.Start();

        Assert.Throws<TournamentInvalidStateException>(() =>
            t.AddParticipant(Guid.NewGuid(), "LateBot", DefaultLineup));
    }

    [Fact]
    public void AddParticipant_AtMaxCapacity_ThrowsTournamentInvalidStateException()
    {
        var t = NewTournament(0, topN: 2, maxParticipants: 2);
        t.AddParticipant(Guid.NewGuid(), "Bot1", DefaultLineup);
        t.AddParticipant(Guid.NewGuid(), "Bot2", DefaultLineup);

        Assert.Throws<TournamentInvalidStateException>(() =>
            t.AddParticipant(Guid.NewGuid(), "Bot3", DefaultLineup));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Start — round-robin schedule generation
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Start_TransitionsStatusToGroupStage()
    {
        var t = NewTournament(2);
        t.Start();
        Assert.Equal(TournamentStatus.GroupStage, t.Status);
    }

    [Fact]
    public void Start_WithFourBots_GeneratesSixGroupMatches()
    {
        // C(4,2) = 6
        var t = NewTournament(4);
        t.Start();
        Assert.Equal(6, t.GroupMatches.Count);
    }

    [Fact]
    public void Start_WithThreeBots_GeneratesThreeGroupMatches()
    {
        // C(3,2) = 3; the bye slot is added internally but produces no real match
        var t = NewTournament(3);
        t.Start();
        Assert.Equal(3, t.GroupMatches.Count);
    }

    [Fact]
    public void Start_FourBots_EachPairPlaysExactlyOnce()
    {
        var t = NewTournament(4);
        t.Start();

        var pairs = t.GroupMatches
            .Select(m => (A: Min(m.BotOne, m.BotTwo), B: Max(m.BotOne, m.BotTwo)))
            .ToList();

        // All pairs unique → no repeated matchup
        Assert.Equal(pairs.Count, pairs.Distinct().Count());
    }

    [Fact]
    public void Start_WhenNotPending_ThrowsTournamentInvalidStateException()
    {
        var t = NewTournament(2);
        t.Start();

        Assert.Throws<TournamentInvalidStateException>(() => t.Start());
    }

    [Fact]
    public void Start_WithFewerThanTwoParticipants_ThrowsTournamentInvalidStateException()
    {
        // Create a tournament with 0 participants (NewTournament with 0 bots)
        var t = NewTournament(0);

        Assert.Throws<TournamentInvalidStateException>(() => t.Start());
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ComputeStandings
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ComputeStandings_Win_GivesThreePoints()
    {
        var t = NewTournament(2);
        t.Start();
        var match = t.GroupMatches[0];
        match.RecordResult(match.BotOne, false, 10, 5);

        var standings = t.ComputeStandings();
        var winner = standings.Single(s => s.BotId == match.BotOne);
        Assert.Equal(3, winner.Points);
    }

    [Fact]
    public void ComputeStandings_Loss_GivesOnePoint()
    {
        var t = NewTournament(2);
        t.Start();
        var match = t.GroupMatches[0];
        match.RecordResult(match.BotOne, false, 10, 5);

        var standings = t.ComputeStandings();
        var loser = standings.Single(s => s.BotId == match.BotTwo);
        Assert.Equal(1, loser.Points);
    }

    [Fact]
    public void ComputeStandings_Draw_GivesTwoPointsEachBot()
    {
        var t = NewTournament(2);
        t.Start();
        var match = t.GroupMatches[0];
        match.RecordResult(null, true, 5, 5);

        var standings = t.ComputeStandings();
        Assert.All(standings, s => Assert.Equal(2, s.Points));
    }

    [Fact]
    public void ComputeStandings_OrderedByPoints_HighestFirst()
    {
        var t = NewTournament(3);
        t.Start();
        // Record BotOne wins in every match
        foreach (var match in t.GroupMatches)
            match.RecordResult(match.BotOne, false, 10, 5);

        var standings = t.ComputeStandings();

        for (int i = 0; i < standings.Count - 1; i++)
            Assert.True(standings[i].Points >= standings[i + 1].Points,
                $"Position {i} ({standings[i].Points} pts) should be >= position {i + 1} ({standings[i + 1].Points} pts)");
    }

    [Fact]
    public void ComputeStandings_TieBreak_ByCoinScoreDescending()
    {
        // Two bots draw but BotOne has a higher coin score → BotOne ranked first
        var t = NewTournament(2);
        t.Start();
        var match = t.GroupMatches[0];
        match.RecordResult(null, true, 10, 5); // draw; BotOne: 10 coins, BotTwo: 5 coins

        var standings = t.ComputeStandings();
        Assert.Equal(match.BotOne, standings[0].BotId);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // IsGroupStageComplete
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void IsGroupStageComplete_AllMatchesCompleted_ReturnsTrue()
    {
        var t = NewTournament(2);
        t.Start();
        CompleteAllGroupMatches(t);
        Assert.True(t.IsGroupStageComplete());
    }

    [Fact]
    public void IsGroupStageComplete_NotAllMatchesCompleted_ReturnsFalse()
    {
        var t = NewTournament(3);
        t.Start();
        // Only complete 2 of the 3 matches
        t.GroupMatches[0].RecordResult(t.GroupMatches[0].BotOne, false, 10, 5);
        t.GroupMatches[1].RecordResult(t.GroupMatches[1].BotOne, false, 10, 5);

        Assert.False(t.IsGroupStageComplete());
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AdvanceToKnockout
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AdvanceToKnockout_TransitionsStatusToKnockoutStage()
    {
        var t = NewTournament(4);
        t.Start();
        CompleteAllGroupMatches(t);
        t.AdvanceToKnockout();
        Assert.Equal(TournamentStatus.KnockoutStage, t.Status);
    }

    [Fact]
    public void AdvanceToKnockout_SelectsTopNBots_FourBotsTopTwo()
    {
        // TopN=2 with 4 bots → bracket has bracketSize=2 → 1 final match in round 1
        var t = NewTournament(4, topN: 2);
        t.Start();
        CompleteAllGroupMatches(t);
        t.AdvanceToKnockout();

        Assert.Single(t.KnockoutMatches);
    }

    [Fact]
    public void AdvanceToKnockout_WhenGroupNotComplete_ThrowsTournamentInvalidStateException()
    {
        var t = NewTournament(4);
        t.Start();
        // Do not complete any group matches

        Assert.Throws<TournamentInvalidStateException>(() => t.AdvanceToKnockout());
    }

    [Fact]
    public void AdvanceToKnockout_ByeMatch_IsAutoResolved()
    {
        // 3 bots, TopN=3 → bracket padded to 4 → 1 bye slot → 1 auto-resolved match
        var t = NewTournament(3, topN: 3);
        t.Start();
        CompleteAllGroupMatches(t);
        t.AdvanceToKnockout();

        var byeMatch = t.KnockoutMatches.SingleOrDefault(m => m.IsBye);
        Assert.NotNull(byeMatch);
        Assert.True(byeMatch.IsCompleted);
        Assert.NotNull(byeMatch.WinnerId);
    }

    [Fact]
    public void AdvanceToKnockout_ByeWinner_PropagatedToFinalSlot()
    {
        // 3 bots: seed1 gets bye; their winner propagates to the final
        var t = NewTournament(3, topN: 3);
        t.Start();
        // All draws so coin score breaks the tie: BotOne of each match gets 10 coins
        foreach (var m in t.GroupMatches)
            m.RecordResult(null, isDraw: true, botOneScore: 10, botTwoScore: 5);

        t.AdvanceToKnockout();

        var byeMatch = t.KnockoutMatches.Single(m => m.IsBye);
        var finalMatch = t.KnockoutMatches.Single(m => m.Round == 2);

        // The non-bye participant advances to the final
        var byeWinner = byeMatch.WinnerId;
        Assert.True(finalMatch.BotOne == byeWinner || finalMatch.BotTwo == byeWinner,
            "Bye winner should be placed in a slot of the final.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // RecordKnockoutResult
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RecordKnockoutResult_Draw_WinnerIsHigherSeed()
    {
        // With 4 bots, TopN=4 → 2 first-round matches + 1 final
        var t = NewTournament(4, topN: 4);
        t.Start();
        CompleteAllGroupMatches(t);
        t.AdvanceToKnockout();

        var r1Match = t.KnockoutMatches.First(m => m.Round == 1);
        t.RecordKnockoutResult(r1Match.Id, null, isDraw: true);

        // BotOne is the higher seed → should be the winner
        Assert.Equal(r1Match.BotOne, r1Match.WinnerId);
    }

    [Fact]
    public void RecordKnockoutResult_WinnerAdvancesToNextRound()
    {
        var t = NewTournament(4, topN: 4);
        t.Start();
        CompleteAllGroupMatches(t);
        t.AdvanceToKnockout();

        var r1P0 = t.KnockoutMatches.Single(m => m.Round == 1 && m.Position == 0);
        t.RecordKnockoutResult(r1P0.Id, null, isDraw: true);

        // Winner (BotOne of r1P0 via draw rule) should appear in the final
        var final = t.KnockoutMatches.Single(m => m.Round == 2);
        Assert.Equal(r1P0.BotOne, final.BotOne);
    }

    [Fact]
    public void RecordKnockoutResult_FinalMatch_TransitionsTournamentToCompleted()
    {
        // 2 bots, TopN=2 → 1 knockout match (the final)
        var t = NewTournament(2, topN: 2);
        t.Start();
        t.GroupMatches[0].RecordResult(null, isDraw: true, botOneScore: 5, botTwoScore: 5);
        t.AdvanceToKnockout();

        var finalMatch = t.KnockoutMatches.Single();
        t.RecordKnockoutResult(finalMatch.Id, null, isDraw: true);

        Assert.Equal(TournamentStatus.Completed, t.Status);
    }

    [Fact]
    public void RecordKnockoutResult_FinalMatch_SetsWinnerId()
    {
        var t = NewTournament(2, topN: 2);
        t.Start();
        t.GroupMatches[0].RecordResult(null, isDraw: true, botOneScore: 5, botTwoScore: 5);
        t.AdvanceToKnockout();

        var finalMatch = t.KnockoutMatches.Single();
        t.RecordKnockoutResult(finalMatch.Id, null, isDraw: true);

        Assert.NotNull(t.WinnerId);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Cancel
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Cancel_FromPending_SetsCancelledStatus()
    {
        var t = NewTournament(2);
        t.Cancel();
        Assert.Equal(TournamentStatus.Cancelled, t.Status);
    }

    [Fact]
    public void Cancel_FromGroupStage_SetsCancelledStatus()
    {
        var t = NewTournament(2);
        t.Start();
        t.Cancel();
        Assert.Equal(TournamentStatus.Cancelled, t.Status);
    }

    [Fact]
    public void Cancel_FromKnockoutStage_SetsCancelledStatus()
    {
        var t = NewTournament(4, topN: 2);
        t.Start();
        CompleteAllGroupMatches(t);
        t.AdvanceToKnockout();

        t.Cancel();

        Assert.Equal(TournamentStatus.Cancelled, t.Status);
    }

    [Fact]
    public void Cancel_WhenCompleted_ThrowsTournamentInvalidStateException()
    {
        var t = NewTournament(2, topN: 2);
        t.Start();
        t.GroupMatches[0].RecordResult(null, isDraw: true, botOneScore: 5, botTwoScore: 5);
        t.AdvanceToKnockout();
        t.RecordKnockoutResult(t.KnockoutMatches.Single().Id, null, isDraw: true);
        // t is now Completed

        Assert.Throws<TournamentInvalidStateException>(() => t.Cancel());
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_ThrowsTournamentInvalidStateException()
    {
        var t = NewTournament(2);
        t.Cancel();

        Assert.Throws<TournamentInvalidStateException>(() => t.Cancel());
    }

    // ══════════════════════════════════════════════════════════════════════════
    // RecordGroupResult (aggregate-level translation)
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RecordGroupResult_WinnerTranslatedFromGamePlayerId_CorrectlyAttributesPoints()
    {
        var t = NewTournament(2);
        t.Start();
        var match = t.GroupMatches[0];

        // Assign a game with known player slot IDs
        var gameId = Guid.NewGuid();
        var botOnePlayerId = Guid.NewGuid();
        match.AssignGame(gameId, botOnePlayerId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Record result passing the game player ID (not the bot ID)
        t.RecordGroupResult(match.Id, botOnePlayerId, isDraw: false, botOneScore: 10, botTwoScore: 3);

        var standings = t.ComputeStandings();
        var winnerStanding = standings.Single(s => s.BotId == match.BotOne);
        Assert.Equal(3, winnerStanding.Points);
    }

    [Fact]
    public void RecordGroupResult_UnknownMatchId_ThrowsDomainException()
    {
        var t = NewTournament(2);
        t.Start();

        Assert.Throws<DomainException>(() =>
            t.RecordGroupResult(Guid.NewGuid(), null, isDraw: true, botOneScore: 5, botTwoScore: 5));
    }
}
