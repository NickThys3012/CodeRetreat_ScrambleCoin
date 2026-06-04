using Microsoft.Extensions.Logging;
using NSubstitute;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Tournament.GetStandings;
using DomainTournament = ScrambleCoin.Domain.Tournaments.Tournament;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Unit tests for <see cref="GetTournamentStandingsQueryHandler"/> (Issue #52).
/// </summary>
public class GetTournamentStandingsQueryHandlerTests
{
    private static readonly IReadOnlyList<string> DefaultLineup =
        ["Mickey", "Minnie", "Donald", "Goofy", "Scrooge"];

    private static GetTournamentStandingsQueryHandler BuildHandler(
        ITournamentRepository tournamentRepo,
        IGameRepository gameRepo,
        IUnitOfWork uow)
    {
        var logger = Substitute.For<ILogger<GetTournamentStandingsQueryHandler>>();
        return new GetTournamentStandingsQueryHandler(tournamentRepo, gameRepo, uow, logger);
    }

    [Fact]
    public async Task Handle_ReturnsDtoWithCorrectTournamentId()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = new DomainTournament(tournamentId, "Test", 8, 4, DateTimeOffset.UtcNow);
        tournament.AddParticipant(Guid.NewGuid(), "Bot1", DefaultLineup);
        tournament.AddParticipant(Guid.NewGuid(), "Bot2", DefaultLineup);

        var repo = Substitute.For<ITournamentRepository>();
        repo.GetByIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(tournament);

        var gameRepo = Substitute.For<IGameRepository>();
        var uow = Substitute.For<IUnitOfWork>();

        var handler = BuildHandler(repo, gameRepo, uow);
        var result = await handler.Handle(
            new GetTournamentStandingsQuery(tournamentId),
            CancellationToken.None);

        Assert.Equal(tournamentId, result.TournamentId);
    }

    [Fact]
    public async Task Handle_ReturnsDtoWithOneEntryPerParticipant()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = new DomainTournament(tournamentId, "Test", 8, 4, DateTimeOffset.UtcNow);
        tournament.AddParticipant(Guid.NewGuid(), "Bot1", DefaultLineup);
        tournament.AddParticipant(Guid.NewGuid(), "Bot2", DefaultLineup);
        tournament.AddParticipant(Guid.NewGuid(), "Bot3", DefaultLineup);

        var repo = Substitute.For<ITournamentRepository>();
        repo.GetByIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(tournament);

        var gameRepo = Substitute.For<IGameRepository>();
        var uow = Substitute.For<IUnitOfWork>();

        var handler = BuildHandler(repo, gameRepo, uow);
        var result = await handler.Handle(
            new GetTournamentStandingsQuery(tournamentId),
            CancellationToken.None);

        Assert.Equal(3, result.Standings.Count);
    }

    [Fact]
    public async Task Handle_ReturnsDtoWithCorrectTournamentName()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = new DomainTournament(tournamentId, "Grand Prix", 8, 4, DateTimeOffset.UtcNow);
        tournament.AddParticipant(Guid.NewGuid(), "Bot1", DefaultLineup);
        tournament.AddParticipant(Guid.NewGuid(), "Bot2", DefaultLineup);

        var repo = Substitute.For<ITournamentRepository>();
        repo.GetByIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(tournament);

        var gameRepo = Substitute.For<IGameRepository>();
        var uow = Substitute.For<IUnitOfWork>();

        var handler = BuildHandler(repo, gameRepo, uow);
        var result = await handler.Handle(
            new GetTournamentStandingsQuery(tournamentId),
            CancellationToken.None);

        Assert.Equal("Grand Prix", result.TournamentName);
    }

    [Fact]
    public async Task Handle_GroupStageNoCompletedGames_AllStandingsAtZeroPoints()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = new DomainTournament(tournamentId, "Test", 8, 4, DateTimeOffset.UtcNow);
        tournament.AddParticipant(Guid.NewGuid(), "Bot1", DefaultLineup);
        tournament.AddParticipant(Guid.NewGuid(), "Bot2", DefaultLineup);
        tournament.Start(); // GroupStage with 1 incomplete match

        var repo = Substitute.For<ITournamentRepository>();
        repo.GetByIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(tournament);

        var gameRepo = Substitute.For<IGameRepository>();
        // No finished games; the match has no GameId so sync is skipped
        var uow = Substitute.For<IUnitOfWork>();

        var handler = BuildHandler(repo, gameRepo, uow);
        var result = await handler.Handle(
            new GetTournamentStandingsQuery(tournamentId),
            CancellationToken.None);

        Assert.All(result.Standings, entry => Assert.Equal(0, entry.Points));
    }

    [Fact]
    public async Task Handle_StandingEntry_HasCorrectRankOneForLeader()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = new DomainTournament(tournamentId, "Test", 8, 4, DateTimeOffset.UtcNow);
        var leadBotId = Guid.NewGuid();
        tournament.AddParticipant(leadBotId, "Lead", DefaultLineup);
        tournament.AddParticipant(Guid.NewGuid(), "Other", DefaultLineup);
        tournament.Start();

        // Complete the group match with leadBot winning
        var match = tournament.GroupMatches[0];
        match.RecordResult(leadBotId, false, 10, 5);

        var repo = Substitute.For<ITournamentRepository>();
        repo.GetByIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(tournament);

        var gameRepo = Substitute.For<IGameRepository>();
        var uow = Substitute.For<IUnitOfWork>();

        var handler = BuildHandler(repo, gameRepo, uow);
        var result = await handler.Handle(
            new GetTournamentStandingsQuery(tournamentId),
            CancellationToken.None);

        var top = result.Standings.Single(s => s.BotId == leadBotId);
        Assert.Equal(1, top.Rank);
    }

    [Fact]
    public async Task Handle_WhenPendingStatus_DoesNotCallGameRepository()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = new DomainTournament(tournamentId, "Test", 8, 4, DateTimeOffset.UtcNow);
        tournament.AddParticipant(Guid.NewGuid(), "Bot1", DefaultLineup);
        tournament.AddParticipant(Guid.NewGuid(), "Bot2", DefaultLineup);
        // Not started → status is Pending, no group matches to sync

        var repo = Substitute.For<ITournamentRepository>();
        repo.GetByIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(tournament);

        var gameRepo = Substitute.For<IGameRepository>();
        var uow = Substitute.For<IUnitOfWork>();

        var handler = BuildHandler(repo, gameRepo, uow);
        await handler.Handle(
            new GetTournamentStandingsQuery(tournamentId),
            CancellationToken.None);

        await gameRepo.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
