using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Tournament;
using ScrambleCoin.Application.Tournament.AddParticipant;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.Tournaments;
using DomainTournament = ScrambleCoin.Domain.Tournaments.Tournament;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Unit tests for <see cref="AddTournamentParticipantCommandHandler"/> (Issue #52).
/// </summary>
public class AddTournamentParticipantCommandHandlerTests
{
    private static readonly IReadOnlyList<string> DefaultLineup =
        ["Mickey", "Minnie", "Donald", "Goofy", "Scrooge"];

    private static AddTournamentParticipantCommandHandler BuildHandler(
        ITournamentRepository repo,
        IUnitOfWork uow)
    {
        var logger = Substitute.For<ILogger<AddTournamentParticipantCommandHandler>>();
        return new AddTournamentParticipantCommandHandler(repo, uow, logger);
    }

    [Fact]
    public async Task Handle_AddsBotToTournament()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = new DomainTournament(tournamentId, "Test", 8, 4, DateTimeOffset.UtcNow);

        var repo = Substitute.For<ITournamentRepository>();
        repo.GetByIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(tournament);

        var uow = Substitute.For<IUnitOfWork>();
        var handler = BuildHandler(repo, uow);

        var botId = Guid.NewGuid();
        await handler.Handle(
            new AddTournamentParticipantCommand(tournamentId, botId, "TestBot", DefaultLineup),
            CancellationToken.None);

        Assert.Single(tournament.Participants);
        Assert.Equal(botId, tournament.Participants[0].BotId);
    }

    [Fact]
    public async Task Handle_SavesTournamentToRepository()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = new DomainTournament(tournamentId, "Test", 8, 4, DateTimeOffset.UtcNow);

        var repo = Substitute.For<ITournamentRepository>();
        repo.GetByIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(tournament);

        var uow = Substitute.For<IUnitOfWork>();
        var handler = BuildHandler(repo, uow);

        await handler.Handle(
            new AddTournamentParticipantCommand(tournamentId, Guid.NewGuid(), "Bot", DefaultLineup),
            CancellationToken.None);

        await repo.Received(1).SaveAsync(
            Arg.Any<DomainTournament>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateBot_PropagatesDomainException()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = new DomainTournament(tournamentId, "Test", 8, 4, DateTimeOffset.UtcNow);
        var botId = Guid.NewGuid();
        tournament.AddParticipant(botId, "FirstBot", DefaultLineup);

        var repo = Substitute.For<ITournamentRepository>();
        repo.GetByIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(tournament);

        var uow = Substitute.For<IUnitOfWork>();
        var handler = BuildHandler(repo, uow);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(
                new AddTournamentParticipantCommand(tournamentId, botId, "DuplicateBot", DefaultLineup),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UnknownTournament_PropagatesTournamentNotFoundException()
    {
        var unknownId = Guid.NewGuid();
        var repo = Substitute.For<ITournamentRepository>();
        repo.GetByIdAsync(unknownId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new TournamentNotFoundException(unknownId));

        var uow = Substitute.For<IUnitOfWork>();
        var handler = BuildHandler(repo, uow);

        await Assert.ThrowsAsync<TournamentNotFoundException>(() =>
            handler.Handle(
                new AddTournamentParticipantCommand(unknownId, Guid.NewGuid(), "Bot", DefaultLineup),
                CancellationToken.None));
    }
}
