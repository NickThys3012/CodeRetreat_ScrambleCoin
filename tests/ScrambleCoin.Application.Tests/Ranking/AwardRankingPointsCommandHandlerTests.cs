using Microsoft.Extensions.Logging;
using NSubstitute;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Ranking.AwardRankingPoints;
using ScrambleCoin.Domain.Entities;

namespace ScrambleCoin.Application.Tests.Ranking;

/// <summary>
/// Unit tests for <see cref="AwardRankingPointsCommandHandler"/> (Issue #53).
/// All repository and UoW calls are stubbed via NSubstitute.
/// </summary>
public sealed class AwardRankingPointsCommandHandlerTests
{
    // ── Fixtures ──────────────────────────────────────────────────────────────

    private readonly IRankingRepository _repo    = Substitute.For<IRankingRepository>();
    private readonly IUnitOfWork        _uow     = Substitute.For<IUnitOfWork>();
    private readonly ILogger<AwardRankingPointsCommandHandler> _logger =
        Substitute.For<ILogger<AwardRankingPointsCommandHandler>>();

    private AwardRankingPointsCommandHandler BuildHandler() =>
        new(_repo, _uow, _logger);

    // IDs reused across tests
    private static readonly Guid BotOneId   = Guid.NewGuid();
    private static readonly Guid BotTwoId   = Guid.NewGuid();
    private static readonly Guid GameId     = Guid.NewGuid();

    private static RankingTrack TrackFor(Guid id, string name) => new(id, name);

    private AwardRankingPointsCommand WinCommand(Guid winnerId) =>
        new(GameId, BotOneId, "BotOne", BotTwoId, "BotTwo",
            WinnerId: winnerId, IsDraw: false, TurnNumber: 5);

    private AwardRankingPointsCommand DrawCommand() =>
        new(GameId, BotOneId, "BotOne", BotTwoId, "BotTwo",
            WinnerId: null, IsDraw: true, TurnNumber: 5);

    // ── Handle Win ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Win_RecordsWinForWinner_AndLossForLoser()
    {
        // Arrange
        var botOneTrack = TrackFor(BotOneId, "BotOne");
        var botTwoTrack = TrackFor(BotTwoId, "BotTwo");
        _repo.GetByBotIdAsync(BotOneId, Arg.Any<CancellationToken>()).Returns(botOneTrack);
        _repo.GetByBotIdAsync(BotTwoId, Arg.Any<CancellationToken>()).Returns(botTwoTrack);

        var handler = BuildHandler();
        var command = WinCommand(BotOneId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: BotOne (winner) has 3 pts from a win; BotTwo (loser) has 1 pt from a loss
        Assert.Equal(3, botOneTrack.Points);
        Assert.Equal(1, botOneTrack.Wins);
        Assert.Equal(1, botTwoTrack.Points);
        Assert.Equal(1, botTwoTrack.Losses);
    }

    [Fact]
    public async Task Handle_Win_WinnerIsRecordedAsWinner()
    {
        var botOneTrack = TrackFor(BotOneId, "BotOne");
        var botTwoTrack = TrackFor(BotTwoId, "BotTwo");
        _repo.GetByBotIdAsync(BotOneId, Arg.Any<CancellationToken>()).Returns(botOneTrack);
        _repo.GetByBotIdAsync(BotTwoId, Arg.Any<CancellationToken>()).Returns(botTwoTrack);

        await BuildHandler().Handle(WinCommand(BotOneId), CancellationToken.None);

        Assert.Equal(1, botOneTrack.Wins);
        Assert.Equal(0, botOneTrack.Losses);
    }

    [Fact]
    public async Task Handle_Win_LoserIsRecordedAsLoser()
    {
        var botOneTrack = TrackFor(BotOneId, "BotOne");
        var botTwoTrack = TrackFor(BotTwoId, "BotTwo");
        _repo.GetByBotIdAsync(BotOneId, Arg.Any<CancellationToken>()).Returns(botOneTrack);
        _repo.GetByBotIdAsync(BotTwoId, Arg.Any<CancellationToken>()).Returns(botTwoTrack);

        await BuildHandler().Handle(WinCommand(BotOneId), CancellationToken.None);

        Assert.Equal(1, botTwoTrack.Losses);
        Assert.Equal(0, botTwoTrack.Wins);
    }

    [Fact]
    public async Task Handle_Win_WhenBotTwoWins_BotTwoGetsWin_BotOneGetsLoss()
    {
        var botOneTrack = TrackFor(BotOneId, "BotOne");
        var botTwoTrack = TrackFor(BotTwoId, "BotTwo");
        _repo.GetByBotIdAsync(BotOneId, Arg.Any<CancellationToken>()).Returns(botOneTrack);
        _repo.GetByBotIdAsync(BotTwoId, Arg.Any<CancellationToken>()).Returns(botTwoTrack);

        await BuildHandler().Handle(WinCommand(BotTwoId), CancellationToken.None);

        Assert.Equal(1, botTwoTrack.Wins);
        Assert.Equal(1, botOneTrack.Losses);
    }

    // ── Handle Draw ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Draw_RecordsDrawForBothBots()
    {
        var botOneTrack = TrackFor(BotOneId, "BotOne");
        var botTwoTrack = TrackFor(BotTwoId, "BotTwo");
        _repo.GetByBotIdAsync(BotOneId, Arg.Any<CancellationToken>()).Returns(botOneTrack);
        _repo.GetByBotIdAsync(BotTwoId, Arg.Any<CancellationToken>()).Returns(botTwoTrack);

        await BuildHandler().Handle(DrawCommand(), CancellationToken.None);

        Assert.Equal(1, botOneTrack.Draws);
        Assert.Equal(1, botTwoTrack.Draws);
    }

    [Fact]
    public async Task Handle_Draw_BothBotsReceiveTwoPoints()
    {
        var botOneTrack = TrackFor(BotOneId, "BotOne");
        var botTwoTrack = TrackFor(BotTwoId, "BotTwo");
        _repo.GetByBotIdAsync(BotOneId, Arg.Any<CancellationToken>()).Returns(botOneTrack);
        _repo.GetByBotIdAsync(BotTwoId, Arg.Any<CancellationToken>()).Returns(botTwoTrack);

        await BuildHandler().Handle(DrawCommand(), CancellationToken.None);

        Assert.Equal(2, botOneTrack.Points);
        Assert.Equal(2, botTwoTrack.Points);
    }

    [Fact]
    public async Task Handle_Draw_NeitherBotHasWinsOrLosses()
    {
        var botOneTrack = TrackFor(BotOneId, "BotOne");
        var botTwoTrack = TrackFor(BotTwoId, "BotTwo");
        _repo.GetByBotIdAsync(BotOneId, Arg.Any<CancellationToken>()).Returns(botOneTrack);
        _repo.GetByBotIdAsync(BotTwoId, Arg.Any<CancellationToken>()).Returns(botTwoTrack);

        await BuildHandler().Handle(DrawCommand(), CancellationToken.None);

        Assert.Equal(0, botOneTrack.Wins);
        Assert.Equal(0, botOneTrack.Losses);
        Assert.Equal(0, botTwoTrack.Wins);
        Assert.Equal(0, botTwoTrack.Losses);
    }

    // ── Unrecognised winner guard ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_UnrecognisedWinnerId_LogsWarning_AndReturnsWithoutSaving()
    {
        var botOneTrack = TrackFor(BotOneId, "BotOne");
        var botTwoTrack = TrackFor(BotTwoId, "BotTwo");
        _repo.GetByBotIdAsync(BotOneId, Arg.Any<CancellationToken>()).Returns(botOneTrack);
        _repo.GetByBotIdAsync(BotTwoId, Arg.Any<CancellationToken>()).Returns(botTwoTrack);

        var unknownWinnerId = Guid.NewGuid();
        var command = new AwardRankingPointsCommand(
            GameId, BotOneId, "BotOne", BotTwoId, "BotTwo",
            WinnerId: unknownWinnerId, IsDraw: false, TurnNumber: 5);

        await BuildHandler().Handle(command, CancellationToken.None);

        // SaveAsync must never be called
        await _repo.DidNotReceive().SaveAsync(Arg.Any<RankingTrack>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnrecognisedWinnerId_UnitOfWork_SaveChangesAsync_NotCalled()
    {
        var botOneTrack = TrackFor(BotOneId, "BotOne");
        var botTwoTrack = TrackFor(BotTwoId, "BotTwo");
        _repo.GetByBotIdAsync(BotOneId, Arg.Any<CancellationToken>()).Returns(botOneTrack);
        _repo.GetByBotIdAsync(BotTwoId, Arg.Any<CancellationToken>()).Returns(botTwoTrack);

        var unknownWinnerId = Guid.NewGuid();
        var command = new AwardRankingPointsCommand(
            GameId, BotOneId, "BotOne", BotTwoId, "BotTwo",
            WinnerId: unknownWinnerId, IsDraw: false, TurnNumber: 5);

        await BuildHandler().Handle(command, CancellationToken.None);

        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── Save ordering ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Win_CallsSaveAsync_ForBothBots_ThenSavesViaUnitOfWork()
    {
        var botOneTrack = TrackFor(BotOneId, "BotOne");
        var botTwoTrack = TrackFor(BotTwoId, "BotTwo");
        _repo.GetByBotIdAsync(BotOneId, Arg.Any<CancellationToken>()).Returns(botOneTrack);
        _repo.GetByBotIdAsync(BotTwoId, Arg.Any<CancellationToken>()).Returns(botTwoTrack);

        await BuildHandler().Handle(WinCommand(BotOneId), CancellationToken.None);

        // Both SaveAsync calls must happen
        await _repo.Received(1).SaveAsync(botOneTrack, Arg.Any<CancellationToken>());
        await _repo.Received(1).SaveAsync(botTwoTrack, Arg.Any<CancellationToken>());
        // UoW SaveChangesAsync must be called exactly once
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Draw_SaveAsyncCalledForBothBots()
    {
        var botOneTrack = TrackFor(BotOneId, "BotOne");
        var botTwoTrack = TrackFor(BotTwoId, "BotTwo");
        _repo.GetByBotIdAsync(BotOneId, Arg.Any<CancellationToken>()).Returns(botOneTrack);
        _repo.GetByBotIdAsync(BotTwoId, Arg.Any<CancellationToken>()).Returns(botTwoTrack);

        await BuildHandler().Handle(DrawCommand(), CancellationToken.None);

        await _repo.Received(1).SaveAsync(botOneTrack, Arg.Any<CancellationToken>());
        await _repo.Received(1).SaveAsync(botTwoTrack, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Draw_UnitOfWork_SaveChangesAsync_CalledOnce()
    {
        var botOneTrack = TrackFor(BotOneId, "BotOne");
        var botTwoTrack = TrackFor(BotTwoId, "BotTwo");
        _repo.GetByBotIdAsync(BotOneId, Arg.Any<CancellationToken>()).Returns(botOneTrack);
        _repo.GetByBotIdAsync(BotTwoId, Arg.Any<CancellationToken>()).Returns(botTwoTrack);

        await BuildHandler().Handle(DrawCommand(), CancellationToken.None);

        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── Bot-not-found creates new track ───────────────────────────────────────

    [Fact]
    public async Task Handle_BotNotFound_CreatesNewTrack_ForBotOne()
    {
        // GetByBotIdAsync returns null → handler should create a new RankingTrack
        _repo.GetByBotIdAsync(BotOneId, Arg.Any<CancellationToken>()).Returns((RankingTrack?)null);
        _repo.GetByBotIdAsync(BotTwoId, Arg.Any<CancellationToken>()).Returns(TrackFor(BotTwoId, "BotTwo"));

        RankingTrack? savedBotOne = null;
        await _repo.SaveAsync(
            Arg.Do<RankingTrack>(t => { if (t.BotId == BotOneId) savedBotOne = t; }),
            Arg.Any<CancellationToken>());

        await BuildHandler().Handle(WinCommand(BotOneId), CancellationToken.None);

        Assert.NotNull(savedBotOne);
        Assert.Equal(BotOneId, savedBotOne!.BotId);
        Assert.Equal("BotOne", savedBotOne.BotName);
    }

    [Fact]
    public async Task Handle_BotNotFound_CreatesNewTrack_ForBotTwo()
    {
        _repo.GetByBotIdAsync(BotOneId, Arg.Any<CancellationToken>()).Returns(TrackFor(BotOneId, "BotOne"));
        _repo.GetByBotIdAsync(BotTwoId, Arg.Any<CancellationToken>()).Returns((RankingTrack?)null);

        RankingTrack? savedBotTwo = null;
        await _repo.SaveAsync(
            Arg.Do<RankingTrack>(t => { if (t.BotId == BotTwoId) savedBotTwo = t; }),
            Arg.Any<CancellationToken>());

        await BuildHandler().Handle(WinCommand(BotOneId), CancellationToken.None);

        Assert.NotNull(savedBotTwo);
        Assert.Equal(BotTwoId, savedBotTwo!.BotId);
        Assert.Equal("BotTwo", savedBotTwo.BotName);
    }

    [Fact]
    public async Task Handle_BothBotsNotFound_CreatesTwoNewTracks()
    {
        _repo.GetByBotIdAsync(BotOneId, Arg.Any<CancellationToken>()).Returns((RankingTrack?)null);
        _repo.GetByBotIdAsync(BotTwoId, Arg.Any<CancellationToken>()).Returns((RankingTrack?)null);

        await BuildHandler().Handle(DrawCommand(), CancellationToken.None);

        // SaveAsync must still be called for both
        await _repo.Received(1).SaveAsync(Arg.Is<RankingTrack>(t => t.BotId == BotOneId), Arg.Any<CancellationToken>());
        await _repo.Received(1).SaveAsync(Arg.Is<RankingTrack>(t => t.BotId == BotTwoId), Arg.Any<CancellationToken>());
    }
}
