using Microsoft.Extensions.Logging;
using NSubstitute;
using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Tournament;
using ScrambleCoin.Application.Tournament.StartTournament;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Tournaments;
using DomainTournament = ScrambleCoin.Domain.Tournaments.Tournament;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Unit tests for <see cref="StartTournamentCommandHandler"/> (Issue #52).
/// </summary>
public class StartTournamentCommandHandlerTests
{
    private static readonly IReadOnlyList<string> DefaultLineup =
        ["Mickey", "Minnie", "Donald", "Goofy", "Scrooge"];

    private static StartTournamentCommandHandler BuildHandler(
        ITournamentRepository tournamentRepo,
        IGameRepository gameRepo,
        IBotRegistrationRepository botRegRepo,
        IUnitOfWork uow)
    {
        var logger = Substitute.For<ILogger<StartTournamentCommandHandler>>();
        return new StartTournamentCommandHandler(tournamentRepo, gameRepo, botRegRepo, uow, logger);
    }

    private static DomainTournament BuildTournamentWithBots(Guid id, int botCount)
    {
        var t = new DomainTournament(id, "Test Cup", 16, 4, DateTimeOffset.UtcNow);
        for (int i = 0; i < botCount; i++)
            t.AddParticipant(Guid.NewGuid(), $"Bot{i}", DefaultLineup);
        return t;
    }

    [Fact]
    public async Task Handle_TransitionsTournamentToGroupStage()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = BuildTournamentWithBots(tournamentId, 2);

        var tournamentRepo = Substitute.For<ITournamentRepository>();
        tournamentRepo.GetByIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(tournament);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRegRepo = Substitute.For<IBotRegistrationRepository>();
        var uow = Substitute.For<IUnitOfWork>();

        var handler = BuildHandler(tournamentRepo, gameRepo, botRegRepo, uow);
        await handler.Handle(new StartTournamentCommand(tournamentId), CancellationToken.None);

        Assert.Equal(TournamentStatus.GroupStage, tournament.Status);
    }

    [Fact]
    public async Task Handle_FourBots_StagesSixGames()
    {
        // C(4,2) = 6 group matches → 6 staged games
        var tournamentId = Guid.NewGuid();
        var tournament = BuildTournamentWithBots(tournamentId, 4);

        var tournamentRepo = Substitute.For<ITournamentRepository>();
        tournamentRepo.GetByIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(tournament);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRegRepo = Substitute.For<IBotRegistrationRepository>();
        var uow = Substitute.For<IUnitOfWork>();

        var handler = BuildHandler(tournamentRepo, gameRepo, botRegRepo, uow);
        await handler.Handle(new StartTournamentCommand(tournamentId), CancellationToken.None);

        await gameRepo.Received(6).StageAsync(Arg.Any<Game>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TwoBots_StagesOneBotRegistrationPerBotPerGame()
    {
        // 1 group match → 2 bot registrations
        var tournamentId = Guid.NewGuid();
        var tournament = BuildTournamentWithBots(tournamentId, 2);

        var tournamentRepo = Substitute.For<ITournamentRepository>();
        tournamentRepo.GetByIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(tournament);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRegRepo = Substitute.For<IBotRegistrationRepository>();
        var uow = Substitute.For<IUnitOfWork>();

        var handler = BuildHandler(tournamentRepo, gameRepo, botRegRepo, uow);
        await handler.Handle(new StartTournamentCommand(tournamentId), CancellationToken.None);

        await botRegRepo.Received(2).StageAsync(
            Arg.Any<Domain.BotRegistrations.BotRegistration>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AssignsGameIdToEachGroupMatch()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = BuildTournamentWithBots(tournamentId, 2);

        var tournamentRepo = Substitute.For<ITournamentRepository>();
        tournamentRepo.GetByIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(tournament);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRegRepo = Substitute.For<IBotRegistrationRepository>();
        var uow = Substitute.For<IUnitOfWork>();

        var handler = BuildHandler(tournamentRepo, gameRepo, botRegRepo, uow);
        await handler.Handle(new StartTournamentCommand(tournamentId), CancellationToken.None);

        Assert.All(tournament.GroupMatches, match => Assert.NotNull(match.GameId));
    }

    [Fact]
    public async Task Handle_CommitsUnitOfWork()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = BuildTournamentWithBots(tournamentId, 2);

        var tournamentRepo = Substitute.For<ITournamentRepository>();
        tournamentRepo.GetByIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(tournament);

        var gameRepo = Substitute.For<IGameRepository>();
        var botRegRepo = Substitute.For<IBotRegistrationRepository>();
        var uow = Substitute.For<IUnitOfWork>();

        var handler = BuildHandler(tournamentRepo, gameRepo, botRegRepo, uow);
        await handler.Handle(new StartTournamentCommand(tournamentId), CancellationToken.None);

        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
