using Microsoft.EntityFrameworkCore;
using ScrambleCoin.Application.Tournament;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Tournaments;
using ScrambleCoin.Infrastructure.Persistence;

namespace ScrambleCoin.Infrastructure.Tests.Persistence;

/// <summary>
/// Infrastructure round-trip tests for <see cref="TournamentRepository"/> using an EF Core InMemory database.
/// Each test gets its own isolated database to avoid state leakage.
/// </summary>
public sealed class TournamentRepositoryTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static DbContextOptions<ScrambleCoinDbContext> BuildOptions(string dbName) =>
        new DbContextOptionsBuilder<ScrambleCoinDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

    /// <summary>Valid piece names recognised by <c>PieceFactory</c>.</summary>
    private static readonly IReadOnlyList<string> ValidLineup =
        ["King", "Queen", "Rook", "Bishop", "Knight"];

    /// <summary>
    /// Creates a <see cref="Tournament"/> with <paramref name="participantCount"/> bots pre-registered.
    /// </summary>
    private static Tournament BuildTournamentWithParticipants(
        int participantCount,
        int maxParticipants = 8,
        int topN = 4)
    {
        var tournament = new Tournament(
            Guid.NewGuid(),
            "Test Tournament",
            maxParticipants,
            topN,
            DateTimeOffset.UtcNow);

        for (int i = 0; i < participantCount; i++)
            tournament.AddParticipant(Guid.NewGuid(), $"Bot{i}", ValidLineup);

        return tournament;
    }

    /// <summary>
    /// Completes all group matches with draws so the tournament can advance to knockout.
    /// </summary>
    private static void CompleteAllGroupMatchesAsDraw(Tournament tournament)
    {
        foreach (var match in tournament.GroupMatches)
        {
            if (!match.IsCompleted)
                match.RecordResult(null, isDraw: true, botOneScore: 5, botTwoScore: 5);
        }
    }

    // ── Test 1 — Pending tournament round-trip ────────────────────────────────

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_PendingTournament_PreservesId()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_PendingTournament_PreservesId));
        var tournament = BuildTournamentWithParticipants(0);

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var repo = new TournamentRepository(ctx);
            await repo.SaveAsync(tournament);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new TournamentRepository(ctx).GetByIdAsync(tournament.Id);
            Assert.Equal(tournament.Id, loaded.Id);
        }
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_PendingTournament_PreservesName()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_PendingTournament_PreservesName));
        var tournament = BuildTournamentWithParticipants(0);

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new TournamentRepository(ctx).SaveAsync(tournament);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new TournamentRepository(ctx).GetByIdAsync(tournament.Id);
            Assert.Equal("Test Tournament", loaded.Name);
        }
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_PendingTournament_PreservesMaxParticipants()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_PendingTournament_PreservesMaxParticipants));
        var tournament = BuildTournamentWithParticipants(0, maxParticipants: 16, topN: 4);

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new TournamentRepository(ctx).SaveAsync(tournament);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new TournamentRepository(ctx).GetByIdAsync(tournament.Id);
            Assert.Equal(16, loaded.MaxParticipants);
        }
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_PendingTournament_PreservesTopN()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_PendingTournament_PreservesTopN));
        var tournament = BuildTournamentWithParticipants(0, maxParticipants: 8, topN: 4);

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new TournamentRepository(ctx).SaveAsync(tournament);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new TournamentRepository(ctx).GetByIdAsync(tournament.Id);
            Assert.Equal(4, loaded.TopN);
        }
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_PendingTournament_PreservesStatus()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_PendingTournament_PreservesStatus));
        var tournament = BuildTournamentWithParticipants(0);

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new TournamentRepository(ctx).SaveAsync(tournament);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new TournamentRepository(ctx).GetByIdAsync(tournament.Id);
            Assert.Equal(TournamentStatus.Pending, loaded.Status);
        }
    }

    // ── Test 2 — GroupStage with participants and assigned group matches ───────

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_GroupStageTournament_PreservesParticipantCount()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_GroupStageTournament_PreservesParticipantCount));
        var tournament = BuildTournamentWithParticipants(4, maxParticipants: 8, topN: 4);
        tournament.Start();

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new TournamentRepository(ctx).SaveAsync(tournament);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new TournamentRepository(ctx).GetByIdAsync(tournament.Id);
            Assert.Equal(4, loaded.Participants.Count);
        }
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_GroupStageTournament_PreservesParticipantNames()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_GroupStageTournament_PreservesParticipantNames));
        var tournament = BuildTournamentWithParticipants(3, maxParticipants: 8, topN: 2);
        tournament.Start();

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new TournamentRepository(ctx).SaveAsync(tournament);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new TournamentRepository(ctx).GetByIdAsync(tournament.Id);
            var names = loaded.Participants.Select(p => p.BotName).OrderBy(n => n).ToList();
            Assert.Equal(["Bot0", "Bot1", "Bot2"], names);
        }
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_GroupStageTournament_PreservesGroupMatchCount()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_GroupStageTournament_PreservesGroupMatchCount));
        // 4 participants → 4C2 = 6 group matches
        var tournament = BuildTournamentWithParticipants(4, maxParticipants: 8, topN: 4);
        tournament.Start();

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new TournamentRepository(ctx).SaveAsync(tournament);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new TournamentRepository(ctx).GetByIdAsync(tournament.Id);
            Assert.Equal(6, loaded.GroupMatches.Count);
        }
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_GroupStageTournament_PreservesAssignedGame()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_GroupStageTournament_PreservesAssignedGame));
        var tournament = BuildTournamentWithParticipants(3, maxParticipants: 8, topN: 2);
        tournament.Start();

        // Assign a game to the first group match
        var firstMatch = tournament.GroupMatches[0];
        var gameId = Guid.NewGuid();
        firstMatch.AssignGame(gameId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new TournamentRepository(ctx).SaveAsync(tournament);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new TournamentRepository(ctx).GetByIdAsync(tournament.Id);
            var loadedMatch = loaded.GroupMatches.Single(m => m.Id == firstMatch.Id);
            Assert.Equal(gameId, loadedMatch.GameId);
        }
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_GroupStageTournament_PreservesParticipantLineup()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_GroupStageTournament_PreservesParticipantLineup));
        var tournament = BuildTournamentWithParticipants(2, maxParticipants: 4, topN: 2);
        tournament.Start();

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new TournamentRepository(ctx).SaveAsync(tournament);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new TournamentRepository(ctx).GetByIdAsync(tournament.Id);
            var lineup = loaded.Participants[0].Lineup;
            Assert.Equal(ValidLineup, lineup);
        }
    }

    // ── Test 3 — KnockoutStage with some results recorded ────────────────────

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_KnockoutStageTournament_PreservesStatus()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_KnockoutStageTournament_PreservesStatus));
        var tournament = BuildTournamentWithParticipants(4, maxParticipants: 8, topN: 4);
        tournament.Start();
        CompleteAllGroupMatchesAsDraw(tournament);
        tournament.AdvanceToKnockout();

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new TournamentRepository(ctx).SaveAsync(tournament);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new TournamentRepository(ctx).GetByIdAsync(tournament.Id);
            Assert.Equal(TournamentStatus.KnockoutStage, loaded.Status);
        }
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_KnockoutStageTournament_PreservesKnockoutMatchCount()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_KnockoutStageTournament_PreservesKnockoutMatchCount));
        // 4 bots in knockout: round 1 = 2 matches, round 2 (final) = 1 match → 3 total
        var tournament = BuildTournamentWithParticipants(4, maxParticipants: 8, topN: 4);
        tournament.Start();
        CompleteAllGroupMatchesAsDraw(tournament);
        tournament.AdvanceToKnockout();

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new TournamentRepository(ctx).SaveAsync(tournament);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new TournamentRepository(ctx).GetByIdAsync(tournament.Id);
            Assert.Equal(3, loaded.KnockoutMatches.Count);
        }
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_KnockoutStageTournament_RecordedResultSurvivesRoundTrip()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_KnockoutStageTournament_RecordedResultSurvivesRoundTrip));
        var tournament = BuildTournamentWithParticipants(4, maxParticipants: 8, topN: 4);
        tournament.Start();
        CompleteAllGroupMatchesAsDraw(tournament);
        tournament.AdvanceToKnockout();

        // Complete the first round 1 match via a draw (isDraw → BotOne wins)
        var round1Match = tournament.KnockoutMatches.First(m => m.Round == 1);
        tournament.RecordKnockoutResult(round1Match.Id, gameWinnerPlayerId: null, isDraw: true);

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new TournamentRepository(ctx).SaveAsync(tournament);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new TournamentRepository(ctx).GetByIdAsync(tournament.Id);
            var loadedMatch = loaded.KnockoutMatches.Single(m => m.Id == round1Match.Id);
            Assert.True(loadedMatch.IsCompleted);
        }
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_KnockoutStageTournament_PreservesKnockoutWinnerId()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_KnockoutStageTournament_PreservesKnockoutWinnerId));
        var tournament = BuildTournamentWithParticipants(4, maxParticipants: 8, topN: 4);
        tournament.Start();
        CompleteAllGroupMatchesAsDraw(tournament);
        tournament.AdvanceToKnockout();

        // Record a result for a round 1 match — winner will be BotOne of that match
        var round1Match = tournament.KnockoutMatches.First(m => m.Round == 1);
        tournament.RecordKnockoutResult(round1Match.Id, gameWinnerPlayerId: null, isDraw: true);
        var expectedWinner = round1Match.WinnerId;

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new TournamentRepository(ctx).SaveAsync(tournament);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new TournamentRepository(ctx).GetByIdAsync(tournament.Id);
            var loadedMatch = loaded.KnockoutMatches.Single(m => m.Id == round1Match.Id);
            Assert.Equal(expectedWinner, loadedMatch.WinnerId);
        }
    }

    // ── Test 4 — Completed tournament with WinnerId ───────────────────────────

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_CompletedTournament_PreservesCompletedStatus()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_CompletedTournament_PreservesCompletedStatus));
        var tournament = BuildAndCompleteTournament();

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new TournamentRepository(ctx).SaveAsync(tournament);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new TournamentRepository(ctx).GetByIdAsync(tournament.Id);
            Assert.Equal(TournamentStatus.Completed, loaded.Status);
        }
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_CompletedTournament_PreservesWinnerId()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_CompletedTournament_PreservesWinnerId));
        var tournament = BuildAndCompleteTournament();
        var expectedWinnerId = tournament.WinnerId;

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new TournamentRepository(ctx).SaveAsync(tournament);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new TournamentRepository(ctx).GetByIdAsync(tournament.Id);
            Assert.Equal(expectedWinnerId, loaded.WinnerId);
        }
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_CompletedTournament_WinnerIdIsNotNull()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_CompletedTournament_WinnerIdIsNotNull));
        var tournament = BuildAndCompleteTournament();

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new TournamentRepository(ctx).SaveAsync(tournament);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new TournamentRepository(ctx).GetByIdAsync(tournament.Id);
            Assert.NotNull(loaded.WinnerId);
        }
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_CompletedTournament_AllKnockoutMatchesCompleted()
    {
        var options = BuildOptions(nameof(SaveAsync_ThenGetByIdAsync_CompletedTournament_AllKnockoutMatchesCompleted));
        var tournament = BuildAndCompleteTournament();

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            await new TournamentRepository(ctx).SaveAsync(tournament);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ScrambleCoinDbContext(options))
        {
            var loaded = await new TournamentRepository(ctx).GetByIdAsync(tournament.Id);
            Assert.All(loaded.KnockoutMatches, m => Assert.True(m.IsCompleted));
        }
    }

    /// <summary>
    /// Builds a minimal 2-participant tournament and drives it to completion.
    /// With 2 bots and TopN=2: group stage has 1 match, knockout has 1 match (the final).
    /// </summary>
    private static Tournament BuildAndCompleteTournament()
    {
        var tournament = new Tournament(
            Guid.NewGuid(),
            "Completed Tournament",
            maxParticipants: 4,
            topN: 2,
            DateTimeOffset.UtcNow);

        tournament.AddParticipant(Guid.NewGuid(), "BotAlpha", ValidLineup);
        tournament.AddParticipant(Guid.NewGuid(), "BotBeta", ValidLineup);

        tournament.Start();

        // Complete the single group match as a draw
        var groupMatch = tournament.GroupMatches.Single();
        groupMatch.RecordResult(null, isDraw: true, botOneScore: 5, botTwoScore: 5);

        // Advance to knockout — produces 1 match (the final) since TopN=2
        tournament.AdvanceToKnockout();

        // Complete the final with a draw — BotOne of the match wins by tie-break
        var final = tournament.KnockoutMatches.Single(m => m.Round == 1);
        tournament.RecordKnockoutResult(final.Id, gameWinnerPlayerId: null, isDraw: true);

        return tournament;
    }
}
