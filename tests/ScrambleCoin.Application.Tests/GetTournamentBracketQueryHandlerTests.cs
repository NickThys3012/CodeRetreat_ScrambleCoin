using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Tournament.GetBracket;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;
using DomainTournament = ScrambleCoin.Domain.Tournaments.Tournament;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Unit tests for <see cref="GetTournamentBracketQueryHandler"/> (Issue #52).
/// </summary>
public class GetTournamentBracketQueryHandlerTests
{
    private static readonly IReadOnlyList<string> DefaultLineup =
        ["Mickey", "Minnie", "Donald", "Goofy", "Scrooge"];

    private static GetTournamentBracketQueryHandler BuildHandler(
        ITournamentRepository tournamentRepo,
        IGameRepository gameRepo,
        IBotRegistrationRepository botRegRepo,
        IPublisher publisher,
        IUnitOfWork uow)
    {
        var logger = Substitute.For<ILogger<GetTournamentBracketQueryHandler>>();
        return new GetTournamentBracketQueryHandler(tournamentRepo, gameRepo, botRegRepo, uow, publisher, logger);
    }

    [Fact]
    public async Task Handle_PendingTournament_ReturnsBracketWithCorrectTournamentId()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = new DomainTournament(tournamentId, "Test", 8, 4, DateTimeOffset.UtcNow);
        tournament.AddParticipant(Guid.NewGuid(), "Bot1", DefaultLineup);
        tournament.AddParticipant(Guid.NewGuid(), "Bot2", DefaultLineup);

        var repo = Substitute.For<ITournamentRepository>();
        repo.GetByIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(tournament);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRegRepo = Substitute.For<IBotRegistrationRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var publisher = Substitute.For<IPublisher>();
        
        var handler = BuildHandler(repo, gameRepo, botRegRepo, publisher, uow);
        var result = await handler.Handle(
            new GetTournamentBracketQuery(tournamentId),
            CancellationToken.None);

        Assert.Equal(tournamentId, result.TournamentId);
    }

    [Fact]
    public async Task Handle_PendingTournament_GroupMatchesEmpty()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = new DomainTournament(tournamentId, "Test", 8, 4, DateTimeOffset.UtcNow);
        tournament.AddParticipant(Guid.NewGuid(), "Bot1", DefaultLineup);
        tournament.AddParticipant(Guid.NewGuid(), "Bot2", DefaultLineup);

        var repo = Substitute.For<ITournamentRepository>();
        repo.GetByIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(tournament);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRegRepo = Substitute.For<IBotRegistrationRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var publisher = Substitute.For<IPublisher>();
        
        var handler = BuildHandler(repo, gameRepo, botRegRepo, publisher, uow);
        var result = await handler.Handle(
            new GetTournamentBracketQuery(tournamentId),
            CancellationToken.None);

        Assert.Empty(result.GroupMatches);
        Assert.Empty(result.KnockoutRounds);
    }

    [Fact]
    public async Task Handle_GroupStage_PopulatesGroupMatchesInDto()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = new DomainTournament(tournamentId, "Test", 8, 4, DateTimeOffset.UtcNow);
        tournament.AddParticipant(Guid.NewGuid(), "Bot1", DefaultLineup);
        tournament.AddParticipant(Guid.NewGuid(), "Bot2", DefaultLineup);
        tournament.Start(); // 1 group match created

        var repo = Substitute.For<ITournamentRepository>();
        repo.GetByIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(tournament);

        // The group match has no GameId, so SyncGroupResults will skip it
        var gameRepo = Substitute.For<IGameRepository>();
        var botRegRepo = Substitute.For<IBotRegistrationRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var publisher = Substitute.For<IPublisher>();
        
        var handler = BuildHandler(repo, gameRepo, botRegRepo, publisher, uow);
        
        var result = await handler.Handle(
            new GetTournamentBracketQuery(tournamentId),
            CancellationToken.None);

        Assert.Single(result.GroupMatches);
    }

    [Fact]
    public async Task Handle_WhenGroupStageComplete_AdvancesToKnockoutStage()
    {
        // 2 bots, TopN=2: 1 group match → complete → bracket has 1 final match
        var tournamentId = Guid.NewGuid();
        var bot1 = Guid.NewGuid();
        var bot2 = Guid.NewGuid();
        var tournament = new DomainTournament(tournamentId, "Test", 8, 2, DateTimeOffset.UtcNow);
        tournament.AddParticipant(bot1, "Bot1", DefaultLineup);
        tournament.AddParticipant(bot2, "Bot2", DefaultLineup);
        tournament.Start();

        // Complete the group match directly (no game involved)
        tournament.GroupMatches[0].RecordResult(bot1, false, 10, 5);

        var repo = Substitute.For<ITournamentRepository>();
        repo.GetByIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(tournament);

        var gameRepo = Substitute.For<IGameRepository>();
        // Knockout game will be staged; GetByIdAsync for the new game is not in the DB yet
        gameRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new GameNotFoundException(Guid.NewGuid()));

        var botRegRepo = Substitute.For<IBotRegistrationRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var publisher = Substitute.For<IPublisher>();
        
        var handler = BuildHandler(repo, gameRepo, botRegRepo, publisher, uow);
        
        var result = await handler.Handle(
            new GetTournamentBracketQuery(tournamentId),
            CancellationToken.None);

        Assert.Equal(nameof(TournamentStatus.KnockoutStage), result.Status);
        Assert.NotEmpty(result.KnockoutRounds);
    }

    [Fact]
    public async Task Handle_WhenGroupStageComplete_SavesTournamentWithChanges()
    {
        var tournamentId = Guid.NewGuid();
        var bot1 = Guid.NewGuid();
        var bot2 = Guid.NewGuid();
        var tournament = new DomainTournament(tournamentId, "Test", 8, 2, DateTimeOffset.UtcNow);
        tournament.AddParticipant(bot1, "Bot1", DefaultLineup);
        tournament.AddParticipant(bot2, "Bot2", DefaultLineup);
        tournament.Start();
        tournament.GroupMatches[0].RecordResult(bot1, false, 10, 5);

        var repo = Substitute.For<ITournamentRepository>();
        repo.GetByIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(tournament);

        var gameRepo = Substitute.For<IGameRepository>();
        gameRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new GameNotFoundException(Guid.NewGuid()));

        var botRegRepo = Substitute.For<IBotRegistrationRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var publisher = Substitute.For<IPublisher>();
        
        var handler = BuildHandler(repo, gameRepo, botRegRepo, publisher, uow);
        
        await handler.Handle(
            new GetTournamentBracketQuery(tournamentId),
            CancellationToken.None);

        await repo.Received(1).SaveAsync(Arg.Any<DomainTournament>(), Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PendingTournament_DoesNotSaveTournament()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = new DomainTournament(tournamentId, "Test", 8, 4, DateTimeOffset.UtcNow);
        tournament.AddParticipant(Guid.NewGuid(), "Bot1", DefaultLineup);
        tournament.AddParticipant(Guid.NewGuid(), "Bot2", DefaultLineup);

        var repo = Substitute.For<ITournamentRepository>();
        repo.GetByIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(tournament);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRegRepo = Substitute.For<IBotRegistrationRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var publisher = Substitute.For<IPublisher>();
        
        var handler = BuildHandler(repo, gameRepo, botRegRepo, publisher, uow);
        
        await handler.Handle(
            new GetTournamentBracketQuery(tournamentId),
            CancellationToken.None);

        await repo.DidNotReceive().SaveAsync(Arg.Any<DomainTournament>(), Arg.Any<CancellationToken>());
    }
}
