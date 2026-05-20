using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Application.Games.MovePiece;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Obstacles;
using ScrambleCoin.Domain.ValueObjects;
using DomainBotReg = ScrambleCoin.Domain.BotRegistrations.BotRegistration;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Integration tests for Charge movement (Issue #45) via the Application layer.
/// Tests the full flow: game creation → piece placement → charge move → score updates.
/// </summary>
public class ChargeMovementIntegrationTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a game in MovePhase with a Charge piece for P1 and a normal piece for P2.
    /// </summary>
    private static (Game game, Guid p1, Guid p2, Piece chargePiece, Piece blocker)
        GameInMovePhaseWithChargePiece(
            Position? chargeStartPos = null,
            Position? blockerPos = null)
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var actualChargeStartPos = chargeStartPos ?? new Position(0, 0);
        var actualBlockerPos = blockerPos ?? new Position(7, 0);

        var chargePiece = new Piece(Guid.NewGuid(), "Charger", p1,
            EntryPointType.Borders, MovementType.Charge, 1, 1);
        var p1Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"P1Fill{i}", p1, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var blockerPiece = new Piece(Guid.NewGuid(), "Blocker", p2,
            EntryPointType.Borders, MovementType.Orthogonal, 1, 1);
        var p2Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"P2Fill{i}", p2, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(new[] { chargePiece }.Concat(p1Fill)));
        game.SetLineup(p2, new Lineup(new[] { blockerPiece }.Concat(p2Fill)));
        game.Start();
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        game.PlacePiece(p1, chargePiece.Id, actualChargeStartPos);
        game.PlacePiece(p2, blockerPiece.Id, actualBlockerPos);

        return (game, p1, p2, chargePiece, blockerPiece);
    }

    /// <summary>
    /// Creates a mock game repository that returns a game by ID and allows saving.
    /// </summary>
    private static IGameRepository MockGameRepository(Game game)
    {
        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        return repo;
    }

    /// <summary>
    /// Creates a mock bot registration repository.
    /// </summary>
    private static IBotRegistrationRepository MockBotRepository(Guid token, Guid playerId, Guid gameId)
    {
        var repo = Substitute.For<IBotRegistrationRepository>();
        repo.GetByTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(new DomainBotReg(token, playerId, gameId));
        return repo;
    }

    /// <summary>
    /// Builds a handler with the provided repositories.
    /// </summary>
    private static MovePieceCommandHandler BuildHandler(
        IGameRepository gameRepo,
        IBotRegistrationRepository botRepo,
        IPublisher? publisher = null)
        => new(gameRepo,
            botRepo,
            publisher ?? Substitute.For<IPublisher>(),
            Substitute.For<ILogger<MovePieceCommandHandler>>());

    /// <summary>
    /// Builds a segment list from a single position (for Charge movement).
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<Position>> BuildChargeSegment(Position directionalStep)
        => new List<IReadOnlyList<Position>>
        {
            new List<Position> { directionalStep }.AsReadOnly()
        }.AsReadOnly();

    // ── Test 1: Charge movement via Application layer ────────────────────────────

    [Fact]
    public async Task ChargeMovement_ViaApplicationLayer_UpdatesGameAndSaves()
    {
        // Arrange: Charge piece at (0,0), blocker at (7,0), charge right
        var (game, p1, _, chargePiece, _) = GameInMovePhaseWithChargePiece(
            chargeStartPos: new Position(0, 0),
            blockerPos: new Position(7, 0));

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act: Charge right (first step to (0,1))
        await handler.Handle(
            new MovePieceCommand(game.Id, token, chargePiece.Id, BuildChargeSegment(new Position(0, 1))),
            CancellationToken.None);

        // Assert: piece slid to board edge at (0,7)
        Assert.Equal(new Position(0, 7), chargePiece.Position);

        // Assert: game was saved
        await gameRepo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChargeMovement_WithCoinsAlongPath_CollectsAllCoinsAndUpdatesScore()
    {
        // Arrange: Charge piece at (0,0), coins at (0,2) and (0,4), blocker at (7,0)
        var (game, p1, _, chargePiece, _) = GameInMovePhaseWithChargePiece(
            chargeStartPos: new Position(0, 0),
            blockerPos: new Position(7, 0));

        // Place coins along the path
        game.Board.GetTile(new Position(0, 2)).SetOccupant(new Coin(CoinType.Silver));
        game.Board.GetTile(new Position(0, 4)).SetOccupant(new Coin(CoinType.Silver));

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act: Charge right
        await handler.Handle(
            new MovePieceCommand(game.Id, token, chargePiece.Id, BuildChargeSegment(new Position(0, 1))),
            CancellationToken.None);

        // Assert: all coins collected (score should be 2)
        Assert.Equal(2, game.Scores[p1]);

        // Assert: tiles are now clear
        Assert.Null(game.Board.GetTile(new Position(0, 2)).AsCoin);
        Assert.Null(game.Board.GetTile(new Position(0, 4)).AsCoin);

        // Assert: game was saved
        await gameRepo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChargeMovement_StoppedByOpponentPiece_StopsBeforePieceAndSaves()
    {
        // Arrange: Charge at (0,0), blocker at (0,5), charge right
        var (game, p1, _, chargePiece, blockerPiece) = GameInMovePhaseWithChargePiece(
            chargeStartPos: new Position(0, 0),
            blockerPos: new Position(0, 5));

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act: Charge right
        await handler.Handle(
            new MovePieceCommand(game.Id, token, chargePiece.Id, BuildChargeSegment(new Position(0, 1))),
            CancellationToken.None);

        // Assert: piece stopped one tile before blocker
        Assert.Equal(new Position(0, 4), chargePiece.Position);
        Assert.Equal(new Position(0, 5), blockerPiece.Position);

        // Assert: game was saved
        await gameRepo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChargeMovement_FirstTileBlocked_DoesNotMoveButCountsAsMoved()
    {
        // Arrange: Charge at (0,0), blocker at (0,1), charge right
        var (game, p1, _, chargePiece, blockerPiece) = GameInMovePhaseWithChargePiece(
            chargeStartPos: new Position(0, 0),
            blockerPos: new Position(0, 1));

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act: Attempt to charge right
        await handler.Handle(
            new MovePieceCommand(game.Id, token, chargePiece.Id, BuildChargeSegment(new Position(0, 1))),
            CancellationToken.None);

        // Assert: piece does not move (stays at origin)
        Assert.Equal(new Position(0, 0), chargePiece.Position);
        Assert.Equal(new Position(0, 1), blockerPiece.Position);

        // Assert: game was saved (move was processed, even though piece didn't move)
        await gameRepo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChargeMovement_MultipleCoinsAlongPath_AllCollected()
    {
        // Arrange: Charge at (0,4), coins at (1,4), (2,4), (3,4), (4,4), blocker at (7,4)
        var (game, p1, _, chargePiece, _) = GameInMovePhaseWithChargePiece(
            chargeStartPos: new Position(0, 4),
            blockerPos: new Position(7, 4));

        // Place coins along the path (1-4 columns)
        for (var row = 1; row <= 4; row++)
            game.Board.GetTile(new Position(row, 4)).SetOccupant(new Coin(CoinType.Silver));

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act: Charge down
        await handler.Handle(
            new MovePieceCommand(game.Id, token, chargePiece.Id, BuildChargeSegment(new Position(1, 4))),
            CancellationToken.None);

        // Assert: stopped before blocker at (7,4) — should be at (6,4)
        Assert.Equal(new Position(6, 4), chargePiece.Position);

        // Assert: all coins collected (4 silver coins = 4 points)
        Assert.Equal(4, game.Scores[p1]);

        // Assert: game was saved
        await gameRepo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChargeMovement_VerticalDirections_StopsCorrectly()
    {
        // Arrange: Charge at (0,0), blocker at (6,0), charge down (south)
        var (game, p1, _, chargePiece, _) = GameInMovePhaseWithChargePiece(
            chargeStartPos: new Position(0, 0),
            blockerPos: new Position(6, 0));

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act: Charge down (first step to (1,0))
        await handler.Handle(
            new MovePieceCommand(game.Id, token, chargePiece.Id, BuildChargeSegment(new Position(1, 0))),
            CancellationToken.None);

        // Assert: piece stopped one tile before blocker
        Assert.Equal(new Position(5, 0), chargePiece.Position);

        // Assert: game was saved
        await gameRepo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChargeMovement_RockObstacle_StopsBeforeRock()
    {
        // Arrange: Charge at (0,0), rock at (0,5), charge right
        var (game, p1, _, chargePiece, _) = GameInMovePhaseWithChargePiece(
            chargeStartPos: new Position(0, 0),
            blockerPos: new Position(7, 0)); // Place blocker out of the way

        game.Board.AddRock(new Rock(new Position(0, 5)));

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act: Charge right
        await handler.Handle(
            new MovePieceCommand(game.Id, token, chargePiece.Id, BuildChargeSegment(new Position(0, 1))),
            CancellationToken.None);

        // Assert: piece stopped one tile before rock at (0,4)
        Assert.Equal(new Position(0, 4), chargePiece.Position);

        // Assert: game was saved
        await gameRepo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }
}
