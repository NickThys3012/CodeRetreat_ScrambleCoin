using Microsoft.Extensions.Logging;
using NSubstitute;
using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Application.Games.GetBoardState;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Factories;
using ScrambleCoin.Domain.ValueObjects;
using DomainBotReg = ScrambleCoin.Domain.BotRegistrations.BotRegistration;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Application-layer tests for Issue #59: <see cref="GetBoardStateQueryHandler"/>
/// must expose <c>AvailableFromTurn</c> on every <see cref="PieceDto"/> it returns.
/// </summary>
public class PieceTurnAvailabilityBoardStateTests
{
    private static (Game game, Guid p1, Guid p2) NewStartedGameWithRestrictedLineup()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        // Mix unrestricted starter pieces with restricted ones so we can assert both branches.
        // Lineup must contain exactly Lineup.RequiredPieceCount (= 5) pieces.
        string[] lineup1 = ["Mickey", "Elsa", "Scar", "Merlin", "Goofy"];
        string[] lineup2 = ["Mickey", "Minnie", "Donald", "Goofy", "Scrooge"];

        var pieces1 = lineup1.Select(n => PieceFactory.Create(n, p1)).ToList();
        var pieces2 = lineup2.Select(n => PieceFactory.Create(n, p2)).ToList();

        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(pieces1));
        game.SetLineup(p2, new Lineup(pieces2));
        game.Start();

        return (game, p1, p2);
    }

    private static GetBoardStateQueryHandler BuildHandlerFor(Game game, DomainBotReg reg)
    {
        var gameRepo = Substitute.For<IGameRepository>();
        var botRepo = Substitute.For<IBotRegistrationRepository>();
        gameRepo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        botRepo.GetByTokenAsync(reg.Token, Arg.Any<CancellationToken>()).Returns(reg);

        var logger = Substitute.For<ILogger<GetBoardStateQueryHandler>>();
        return new GetBoardStateQueryHandler(gameRepo, botRepo, logger);
    }

    [Fact]
    public async Task Handle_YourPieces_IncludesAvailableFromTurnForRestrictedPiece()
    {
        var (game, p1, _) = NewStartedGameWithRestrictedLineup();
        var reg = new DomainBotReg(Guid.NewGuid(), p1, game.Id);
        var handler = BuildHandlerFor(game, reg);

        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg.Token), CancellationToken.None);

        var elsa = dto.YourPieces.Single(p => p.Name == "Elsa");
        Assert.Equal(2, elsa.AvailableFromTurn);
    }

    [Fact]
    public async Task Handle_YourPieces_NullAvailableFromTurnForUnrestrictedPiece()
    {
        var (game, p1, _) = NewStartedGameWithRestrictedLineup();
        var reg = new DomainBotReg(Guid.NewGuid(), p1, game.Id);
        var handler = BuildHandlerFor(game, reg);

        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg.Token), CancellationToken.None);

        var mickey = dto.YourPieces.Single(p => p.Name == "Mickey");
        Assert.Null(mickey.AvailableFromTurn);
    }

    [Theory]
    [InlineData("Scar", 3)]
    [InlineData("Merlin", 4)]
    public async Task Handle_YourPieces_ExposesEachRestrictedPieceCorrectly(string name, int expected)
    {
        var (game, p1, _) = NewStartedGameWithRestrictedLineup();
        var reg = new DomainBotReg(Guid.NewGuid(), p1, game.Id);
        var handler = BuildHandlerFor(game, reg);

        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg.Token), CancellationToken.None);

        var piece = dto.YourPieces.Single(p => p.Name == name);
        Assert.Equal(expected, piece.AvailableFromTurn);
    }

    [Fact]
    public async Task Handle_OpponentPieces_AlsoExposesAvailableFromTurn()
    {
        var (game, p1, _) = NewStartedGameWithRestrictedLineup();
        var reg = new DomainBotReg(Guid.NewGuid(), p1, game.Id);
        var handler = BuildHandlerFor(game, reg);

        var dto = await handler.Handle(new GetBoardStateQuery(game.Id, reg.Token), CancellationToken.None);

        // Opponent lineup is all starters → every piece must have null AvailableFromTurn.
        Assert.All(dto.OpponentPieces, p => Assert.Null(p.AvailableFromTurn));
    }
}
