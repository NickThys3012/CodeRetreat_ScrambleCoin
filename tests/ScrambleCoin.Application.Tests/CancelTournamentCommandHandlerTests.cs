using Microsoft.Extensions.Logging;
using NSubstitute;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Tournament;
using ScrambleCoin.Application.Tournament.CancelTournament;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using DomainTournament = ScrambleCoin.Domain.Tournaments.Tournament;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Unit tests for <see cref="CancelTournamentCommandHandler"/> (Issue #52).
/// </summary>
public class CancelTournamentCommandHandlerTests
{
    private static readonly IReadOnlyList<string> DefaultLineup =
        ["Mickey", "Minnie", "Donald", "Goofy", "Scrooge"];

    private static CancelTournamentCommandHandler BuildHandler(
        ITournamentRepository tournamentRepo,
        IGameRepository gameRepo,
        IUnitOfWork uow)
    {
        var logger = Substitute.For<ILogger<CancelTournamentCommandHandler>>();
        return new CancelTournamentCommandHandler(tournamentRepo, gameRepo, uow, logger);
    }

    [Fact]
    public async Task Handle_SetsTournamentStatusToCancelled()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = new DomainTournament(tournamentId, "Test", 8, 4, DateTimeOffset.UtcNow);

        var tournamentRepo = Substitute.For<ITournamentRepository>();
        tournamentRepo.GetByIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(tournament);

        var gameRepo = Substitute.For<IGameRepository>();
        var uow = Substitute.For<IUnitOfWork>();

        var handler = BuildHandler(tournamentRepo, gameRepo, uow);
        await handler.Handle(new CancelTournamentCommand(tournamentId), CancellationToken.None);

        Assert.Equal(TournamentStatus.Cancelled, tournament.Status);
    }

    [Fact]
    public async Task Handle_SavesTournamentToRepository()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = new DomainTournament(tournamentId, "Test", 8, 4, DateTimeOffset.UtcNow);

        var tournamentRepo = Substitute.For<ITournamentRepository>();
        tournamentRepo.GetByIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(tournament);

        var gameRepo = Substitute.For<IGameRepository>();
        var uow = Substitute.For<IUnitOfWork>();

        var handler = BuildHandler(tournamentRepo, gameRepo, uow);
        await handler.Handle(new CancelTournamentCommand(tournamentId), CancellationToken.None);

        await tournamentRepo.Received(1).SaveAsync(
            Arg.Any<DomainTournament>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithActiveGroupGame_ForceCancelsGame()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = new DomainTournament(tournamentId, "Test", 8, 4, DateTimeOffset.UtcNow);
        tournament.AddParticipant(Guid.NewGuid(), "Bot1", DefaultLineup);
        tournament.AddParticipant(Guid.NewGuid(), "Bot2", DefaultLineup);
        tournament.Start();

        // Assign a game to the only group match
        var matchGameId = Guid.NewGuid();
        var match = tournament.GroupMatches[0];
        match.AssignGame(matchGameId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Build a real in-memory game in WaitingForBots state (not yet terminal)
        var gameInDb = new Game(matchGameId, Guid.NewGuid(), Guid.NewGuid(), new Board());

        var tournamentRepo = Substitute.For<ITournamentRepository>();
        tournamentRepo.GetByIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(tournament);

        var gameRepo = Substitute.For<IGameRepository>();
        gameRepo.GetByIdAsync(matchGameId, Arg.Any<CancellationToken>())
            .Returns(gameInDb);

        var uow = Substitute.For<IUnitOfWork>();

        var handler = BuildHandler(tournamentRepo, gameRepo, uow);
        await handler.Handle(new CancelTournamentCommand(tournamentId), CancellationToken.None);

        // Game should be staged after being cancelled
        await gameRepo.Received(1).StageAsync(
            Arg.Is<Game>(g => g.Id == matchGameId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithAlreadyFinishedGame_DoesNotRestageGame()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = new DomainTournament(tournamentId, "Test", 8, 4, DateTimeOffset.UtcNow);
        tournament.AddParticipant(Guid.NewGuid(), "Bot1", DefaultLineup);
        tournament.AddParticipant(Guid.NewGuid(), "Bot2", DefaultLineup);
        tournament.Start();

        var matchGameId = Guid.NewGuid();
        var match = tournament.GroupMatches[0];
        match.AssignGame(matchGameId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        // Mark the match as completed so no game ID appears as active
        match.RecordResult(match.BotOne, false, 10, 5);

        var tournamentRepo = Substitute.For<ITournamentRepository>();
        tournamentRepo.GetByIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(tournament);

        var gameRepo = Substitute.For<IGameRepository>();
        var uow = Substitute.For<IUnitOfWork>();

        var handler = BuildHandler(tournamentRepo, gameRepo, uow);
        await handler.Handle(new CancelTournamentCommand(tournamentId), CancellationToken.None);

        // Completed group matches are filtered out; game repo should NOT be queried
        await gameRepo.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CommitsUnitOfWork()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = new DomainTournament(tournamentId, "Test", 8, 4, DateTimeOffset.UtcNow);

        var tournamentRepo = Substitute.For<ITournamentRepository>();
        tournamentRepo.GetByIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(tournament);

        var gameRepo = Substitute.For<IGameRepository>();
        var uow = Substitute.For<IUnitOfWork>();

        var handler = BuildHandler(tournamentRepo, gameRepo, uow);
        await handler.Handle(new CancelTournamentCommand(tournamentId), CancellationToken.None);

        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithActiveKnockoutGame_ForceCancelsGame()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = new DomainTournament(tournamentId, "Test", 8, 4, DateTimeOffset.UtcNow);
        tournament.AddParticipant(Guid.NewGuid(), "Bot1", DefaultLineup);
        tournament.AddParticipant(Guid.NewGuid(), "Bot2", DefaultLineup);
        tournament.AddParticipant(Guid.NewGuid(), "Bot3", DefaultLineup);
        tournament.AddParticipant(Guid.NewGuid(), "Bot4", DefaultLineup);
        tournament.Start();

        // Complete all group matches so we can advance to knockout
        foreach (var m in tournament.GroupMatches)
            m.RecordResult(m.BotOne, false, 10, 5);

        tournament.AdvanceToKnockout();

        // Assign a game to the first incomplete knockout match
        var knockoutMatch = tournament.KnockoutMatches.First(m => !m.IsCompleted);
        var matchGameId = Guid.NewGuid();
        knockoutMatch.AssignGame(matchGameId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Build an in-memory game in a non-terminal state
        var gameInDb = new Game(matchGameId, Guid.NewGuid(), Guid.NewGuid(), new Board());

        var tournamentRepo = Substitute.For<ITournamentRepository>();
        tournamentRepo.GetByIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(tournament);

        var gameRepo = Substitute.For<IGameRepository>();
        gameRepo.GetByIdAsync(matchGameId, Arg.Any<CancellationToken>())
            .Returns(gameInDb);

        var uow = Substitute.For<IUnitOfWork>();

        var handler = BuildHandler(tournamentRepo, gameRepo, uow);
        await handler.Handle(new CancelTournamentCommand(tournamentId), CancellationToken.None);

        await gameRepo.Received(1).StageAsync(
            Arg.Is<Game>(g => g.Id == matchGameId),
            Arg.Any<CancellationToken>());
    }
}
