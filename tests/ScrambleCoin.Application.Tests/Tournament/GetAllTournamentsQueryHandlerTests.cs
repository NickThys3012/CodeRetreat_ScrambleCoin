using NSubstitute;
using ScrambleCoin.Application.Tournament;
using ScrambleCoin.Application.Tournament.GetAllTournaments;
using ScrambleCoin.Domain.Enums;
using DomainTournament = ScrambleCoin.Domain.Tournaments.Tournament;

namespace ScrambleCoin.Application.Tests.Tournament;

/// <summary>
/// Unit tests for <see cref="GetAllTournamentsQueryHandler"/> (Issue #57).
/// </summary>
public sealed class GetAllTournamentsQueryHandlerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private readonly ITournamentRepository _repo =
        Substitute.For<ITournamentRepository>();

    private GetAllTournamentsQueryHandler BuildHandler() =>
        new(_repo);

    private static DomainTournament MakeTournament(
        string name         = "Test Tournament",
        int maxParticipants = 8,
        int topN            = 4,
        DateTimeOffset? createdAt = null)
    {
        return new DomainTournament(
            id:              Guid.NewGuid(),
            name:            name,
            maxParticipants: maxParticipants,
            topN:            topN,
            createdAtUtc:    createdAt ?? DateTimeOffset.UtcNow);
    }

    // ── Ordering ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsAllTournaments_OrderedByCreatedAtDescending()
    {
        // Arrange: three tournaments with distinct creation times.
        var oldest = MakeTournament("Alpha",  createdAt: DateTimeOffset.UtcNow.AddDays(-3));
        var middle = MakeTournament("Beta",   createdAt: DateTimeOffset.UtcNow.AddDays(-2));
        var newest = MakeTournament("Gamma",  createdAt: DateTimeOffset.UtcNow.AddDays(-1));

        // Repository returns them in arbitrary order.
        _repo.GetAllAsync(Arg.Any<CancellationToken>())
             .Returns((IReadOnlyList<DomainTournament>)new[] { oldest, newest, middle }.ToList().AsReadOnly());

        // Act
        var result = await BuildHandler().Handle(new GetAllTournamentsQuery(), CancellationToken.None);

        // Assert: newest first.
        Assert.Equal(newest.Id, result[0].TournamentId);
        Assert.Equal(middle.Id, result[1].TournamentId);
        Assert.Equal(oldest.Id, result[2].TournamentId);
    }

    // ── Empty state ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenNoTournaments_ReturnsEmptyList()
    {
        // Arrange
        _repo.GetAllAsync(Arg.Any<CancellationToken>())
             .Returns((IReadOnlyList<DomainTournament>)new List<DomainTournament>().AsReadOnly());

        // Act
        var result = await BuildHandler().Handle(new GetAllTournamentsQuery(), CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    // ── DTO mapping ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_MapsTournamentFieldsCorrectly()
    {
        // Arrange
        var now        = DateTimeOffset.UtcNow;
        var tournament = MakeTournament("Grand Prix", maxParticipants: 16, topN: 4, createdAt: now);

        _repo.GetAllAsync(Arg.Any<CancellationToken>())
             .Returns((IReadOnlyList<DomainTournament>)new[] { tournament }.ToList().AsReadOnly());

        // Act
        var result = await BuildHandler().Handle(new GetAllTournamentsQuery(), CancellationToken.None);

        // Assert: all DTO fields map correctly.
        var dto = result.Single();
        Assert.Equal(tournament.Id,             dto.TournamentId);
        Assert.Equal("Grand Prix",              dto.Name);
        Assert.Equal(TournamentStatus.Pending.ToString(), dto.Status);
        Assert.Equal(0,                         dto.ParticipantCount); // no participants added
        Assert.Equal(16,                        dto.MaxParticipants);
        Assert.Equal(now,                       dto.CreatedAtUtc);
    }

    [Fact]
    public async Task Handle_MapsStatusString_MatchesTournamentStatusEnum()
    {
        // Arrange: a freshly created tournament starts in Pending status.
        var tournament = MakeTournament();

        _repo.GetAllAsync(Arg.Any<CancellationToken>())
             .Returns((IReadOnlyList<DomainTournament>)new[] { tournament }.ToList().AsReadOnly());

        // Act
        var result = await BuildHandler().Handle(new GetAllTournamentsQuery(), CancellationToken.None);

        // Assert
        Assert.Equal("Pending", result.Single().Status);
    }

    // ── Repository interaction ────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CallsRepositoryExactlyOnce()
    {
        // Arrange
        _repo.GetAllAsync(Arg.Any<CancellationToken>())
             .Returns((IReadOnlyList<DomainTournament>)new List<DomainTournament>().AsReadOnly());

        var handler = BuildHandler();

        // Act
        await handler.Handle(new GetAllTournamentsQuery(), CancellationToken.None);

        // Assert
        await _repo.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SingleTournament_HasCorrectParticipantCount()
    {
        // Arrange: tournament with no participants added yet.
        var tournament = MakeTournament(maxParticipants: 8, topN: 4);

        _repo.GetAllAsync(Arg.Any<CancellationToken>())
             .Returns((IReadOnlyList<DomainTournament>)new[] { tournament }.ToList().AsReadOnly());

        // Act
        var result = await BuildHandler().Handle(new GetAllTournamentsQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(0, result.Single().ParticipantCount);
        Assert.Equal(8, result.Single().MaxParticipants);
    }
}
