using Microsoft.Extensions.Logging;
using NSubstitute;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Tournament;
using ScrambleCoin.Application.Tournament.CreateTournament;
using ScrambleCoin.Domain.Enums;
using DomainTournament = ScrambleCoin.Domain.Tournaments.Tournament;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Unit tests for <see cref="CreateTournamentCommandHandler"/> (Issue #52).
/// </summary>
public class CreateTournamentCommandHandlerTests
{
    private static CreateTournamentCommandHandler BuildHandler(
        ITournamentRepository repo,
        IUnitOfWork uow)
    {
        var logger = Substitute.For<ILogger<CreateTournamentCommandHandler>>();
        return new CreateTournamentCommandHandler(repo, uow, logger);
    }

    [Fact]
    public async Task Handle_ReturnsNonEmptyTournamentId()
    {
        var repo = Substitute.For<ITournamentRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var handler = BuildHandler(repo, uow);

        var result = await handler.Handle(
            new CreateTournamentCommand("Test Cup", 8),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.TournamentId);
    }

    [Fact]
    public async Task Handle_SavesTournamentToRepository()
    {
        var repo = Substitute.For<ITournamentRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var handler = BuildHandler(repo, uow);

        await handler.Handle(
            new CreateTournamentCommand("Test Cup", 8),
            CancellationToken.None);

        await repo.Received(1).SaveAsync(
            Arg.Is<DomainTournament>(t => t != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SavedTournament_HasPendingStatus()
    {
        DomainTournament? captured = null;
        var repo = Substitute.For<ITournamentRepository>();
        await repo.SaveAsync(
            Arg.Do<DomainTournament>(t => captured = t),
            Arg.Any<CancellationToken>());

        var uow = Substitute.For<IUnitOfWork>();
        var handler = BuildHandler(repo, uow);

        await handler.Handle(
            new CreateTournamentCommand("Test Cup", 8),
            CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(TournamentStatus.Pending, captured!.Status);
    }

    [Fact]
    public async Task Handle_SavedTournament_HasCorrectName()
    {
        DomainTournament? captured = null;
        var repo = Substitute.For<ITournamentRepository>();
        await repo.SaveAsync(
            Arg.Do<DomainTournament>(t => captured = t),
            Arg.Any<CancellationToken>());

        var uow = Substitute.For<IUnitOfWork>();
        var handler = BuildHandler(repo, uow);

        await handler.Handle(
            new CreateTournamentCommand("Grand Prix", 16),
            CancellationToken.None);

        Assert.Equal("Grand Prix", captured!.Name);
    }

    [Fact]
    public async Task Handle_CommitsUnitOfWork()
    {
        var repo = Substitute.For<ITournamentRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var handler = BuildHandler(repo, uow);

        await handler.Handle(
            new CreateTournamentCommand("Test Cup", 8),
            CancellationToken.None);

        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
