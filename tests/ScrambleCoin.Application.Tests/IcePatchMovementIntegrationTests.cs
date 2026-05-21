using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Application.Games.MovePiece;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.ValueObjects;
using DomainBotReg = ScrambleCoin.Domain.BotRegistrations.BotRegistration;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Integration tests for ice patch mechanics (Issue #47).
/// Tests Elsa ice patch placement, piece sliding, blocking conditions,
/// and game state persistence via the MovePieceCommandHandler.
/// </summary>
public class IcePatchMovementIntegrationTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a game in MovePhase with Elsa positioned on a valid border tile.
    /// Returns (game, p1, p2, elsaPiece, p2Piece).
    /// </summary>
    private static (Game game, Guid p1, Guid p2, Piece elsaPiece, Piece p2Piece)
        GameInMovePhaseWithElsa(
            Position? elsaStartPos = null,
            Position? p2StartPos = null)
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var actualElsaStartPos = elsaStartPos ?? new Position(0, 0);
        var actualP2StartPos = p2StartPos ?? new Position(7, 7);

        var elsaPiece = new Piece(Guid.NewGuid(), "Elsa", p1,
            EntryPointType.Borders, MovementType.Orthogonal, 4, 1);

        var p1Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"P1Fill{i}", p1, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var p2Piece = new Piece(Guid.NewGuid(), "P2Piece", p2,
            EntryPointType.Borders, MovementType.Orthogonal, 1, 1);
        var p2Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"P2Fill{i}", p2, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(new[] { elsaPiece }.Concat(p1Fill)));
        game.SetLineup(p2, new Lineup(new[] { p2Piece }.Concat(p2Fill)));
        game.Start();
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        game.PlacePiece(p1, elsaPiece.Id, actualElsaStartPos);
        game.PlacePiece(p2, p2Piece.Id, actualP2StartPos);

        return (game, p1, p2, elsaPiece, p2Piece);
    }

    /// <summary>
    /// Creates a game in MovePhase with a regular piece on a valid border tile for sliding tests.
    /// Returns (game, p1, p2, regularPiece).
    /// </summary>
    private static (Game game, Guid p1, Guid p2, Piece regularPiece)
        GameInMovePhaseWithRegularPiece(
            Position? regularStartPos = null,
            Position? p2StartPos = null)
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var actualRegularStartPos = regularStartPos ?? new Position(0, 1);
        var actualP2StartPos = p2StartPos ?? new Position(7, 7);

        var regularPiece = new Piece(Guid.NewGuid(), "Regular", p1,
            EntryPointType.Borders, MovementType.Orthogonal, 1, 1);

        var p1Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"P1Fill{i}", p1, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var p2Piece = new Piece(Guid.NewGuid(), "P2Piece", p2,
            EntryPointType.Borders, MovementType.Orthogonal, 1, 1);
        var p2Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"P2Fill{i}", p2, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(new[] { regularPiece }.Concat(p1Fill)));
        game.SetLineup(p2, new Lineup(new[] { p2Piece }.Concat(p2Fill)));
        game.Start();
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        game.PlacePiece(p1, regularPiece.Id, actualRegularStartPos);
        game.PlacePiece(p2, p2Piece.Id, actualP2StartPos);

        return (game, p1, p2, regularPiece);
    }

    /// <summary>
    /// Creates a game in MovePhase with a Jump piece on a valid border tile.
    /// Returns (game, p1, p2, jumpPiece).
    /// </summary>
    private static (Game game, Guid p1, Guid p2, Piece jumpPiece)
        GameInMovePhaseWithJumpPiece(
            Position? jumpStartPos = null,
            Position? p2StartPos = null)
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var actualJumpStartPos = jumpStartPos ?? new Position(0, 0);
        var actualP2StartPos = p2StartPos ?? new Position(7, 7);

        var jumpPiece = new Piece(Guid.NewGuid(), "Jump", p1,
            EntryPointType.Borders, MovementType.Jump, 1, 1);

        var p1Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"P1Fill{i}", p1, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var p2Piece = new Piece(Guid.NewGuid(), "P2Piece", p2,
            EntryPointType.Borders, MovementType.Orthogonal, 1, 1);
        var p2Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"P2Fill{i}", p2, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(new[] { jumpPiece }.Concat(p1Fill)));
        game.SetLineup(p2, new Lineup(new[] { p2Piece }.Concat(p2Fill)));
        game.Start();
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        game.PlacePiece(p1, jumpPiece.Id, actualJumpStartPos);
        game.PlacePiece(p2, p2Piece.Id, actualP2StartPos);

        return (game, p1, p2, jumpPiece);
    }

    /// <summary>
    /// Creates a game in MovePhase with a Charge piece on a valid border tile.
    /// Returns (game, p1, p2, chargePiece).
    /// </summary>
    private static (Game game, Guid p1, Guid p2, Piece chargePiece)
        GameInMovePhaseWithChargePiece(
            Position? chargeStartPos = null,
            Position? p2StartPos = null)
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var actualChargeStartPos = chargeStartPos ?? new Position(0, 0);
        var actualP2StartPos = p2StartPos ?? new Position(7, 7);

        var chargePiece = new Piece(Guid.NewGuid(), "Charge", p1,
            EntryPointType.Borders, MovementType.Charge, 1, 1);

        var p1Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"P1Fill{i}", p1, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var p2Piece = new Piece(Guid.NewGuid(), "P2Piece", p2,
            EntryPointType.Borders, MovementType.Orthogonal, 1, 1);
        var p2Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"P2Fill{i}", p2, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(new[] { chargePiece }.Concat(p1Fill)));
        game.SetLineup(p2, new Lineup(new[] { p2Piece }.Concat(p2Fill)));
        game.Start();
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        game.PlacePiece(p1, chargePiece.Id, actualChargeStartPos);
        game.PlacePiece(p2, p2Piece.Id, actualP2StartPos);

        return (game, p1, p2, chargePiece);
    }

    /// <summary>
    /// Creates a game in MovePhase with an Ethereal piece on a valid border tile.
    /// Returns (game, p1, p2, etherealPiece).
    /// </summary>
    private static (Game game, Guid p1, Guid p2, Piece etherealPiece)
        GameInMovePhaseWithEtherealPiece(
            Position? etherealStartPos = null,
            Position? p2StartPos = null)
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var actualEtherealStartPos = etherealStartPos ?? new Position(0, 0);
        var actualP2StartPos = p2StartPos ?? new Position(7, 7);

        var etherealPiece = new Piece(Guid.NewGuid(), "Ethereal", p1,
            EntryPointType.Borders, MovementType.Ethereal, 1, 1);

        var p1Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"P1Fill{i}", p1, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var p2Piece = new Piece(Guid.NewGuid(), "P2Piece", p2,
            EntryPointType.Borders, MovementType.Orthogonal, 1, 1);
        var p2Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"P2Fill{i}", p2, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(new[] { etherealPiece }.Concat(p1Fill)));
        game.SetLineup(p2, new Lineup(new[] { p2Piece }.Concat(p2Fill)));
        game.Start();
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        game.PlacePiece(p1, etherealPiece.Id, actualEtherealStartPos);
        game.PlacePiece(p2, p2Piece.Id, actualP2StartPos);

        return (game, p1, p2, etherealPiece);
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
        IBotRegistrationRepository botRepo)
        => new(gameRepo,
            botRepo,
            Substitute.For<IPublisher>(),
            Substitute.For<ILogger<MovePieceCommandHandler>>());

    /// <summary>
    /// Builds a segment list for orthogonal movement (one step).
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<Position>> BuildSegment(Position step)
        => new List<IReadOnlyList<Position>>
        {
            new List<Position> { step }.AsReadOnly()
        }.AsReadOnly();

    /// <summary>
    /// Builds a multi-step segment for orthogonal movement.
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<Position>> BuildMultiSegment(params Position[] steps)
        => new List<IReadOnlyList<Position>>
        {
            steps.ToList().AsReadOnly()
        }.AsReadOnly();

    // ── Test Cases ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ElsaPlacesIcePatches_OnIntermediateTiles_ExcludingStartAndEnd()
    {
        // Arrange: Elsa at (0,0), will move right to (0,3).
        // Intermediate tiles: (0,1) and (0,2) should receive ice patches.
        var (game, p1, _, elsaPiece, _) = GameInMovePhaseWithElsa();

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act: Elsa moves across the top edge.
        await handler.Handle(
            new MovePieceCommand(game.Id, token, elsaPiece.Id,
                BuildMultiSegment(new Position(0, 1), new Position(0, 2), new Position(0, 3))),
            CancellationToken.None);

        // Assert: patches at intermediate tiles.
        Assert.True(game.Board.HasIcePatch(new Position(0, 1)));
        Assert.True(game.Board.HasIcePatch(new Position(0, 2)));

        // Patches should NOT be at start and end positions.
        Assert.False(game.Board.HasIcePatch(new Position(0, 0)));
        Assert.False(game.Board.HasIcePatch(new Position(0, 3)));

        await gameRepo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegularPiece_SlidesOnIcePatch_ToAdjacentTile()
    {
        // Arrange: the regular piece starts at (0,1), lands on ice at (0,2), then slides to (0,3).
        var (game, p1, _, regularPiece) = GameInMovePhaseWithRegularPiece();
        game.Board.PlaceIcePatch(new Position(0, 2));

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act.
        await handler.Handle(
            new MovePieceCommand(game.Id, token, regularPiece.Id, BuildSegment(new Position(0, 2))),
            CancellationToken.None);

        Assert.Equal(new Position(0, 3), regularPiece.Position);
        await gameRepo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegularPiece_BlockedFromSliding_AtBoardEdge()
    {
        // Arrange: start next to the corner so the slide would leave the board.
        var (game, p1, _, regularPiece) = GameInMovePhaseWithRegularPiece(
            regularStartPos: new Position(0, 6));
        game.Board.PlaceIcePatch(new Position(0, 7));

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act.
        await handler.Handle(
            new MovePieceCommand(game.Id, token, regularPiece.Id, BuildSegment(new Position(0, 7))),
            CancellationToken.None);

        Assert.Equal(new Position(0, 7), regularPiece.Position);
    }

    [Fact]
    public async Task RegularPiece_BlockedFromSliding_ByOpponentPiece()
    {
        // Arrange: the opponent already occupies the slide destination.
        var (game, p1, _, regularPiece) = GameInMovePhaseWithRegularPiece(
            p2StartPos: new Position(0, 3));
        game.Board.PlaceIcePatch(new Position(0, 2));

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act.
        await handler.Handle(
            new MovePieceCommand(game.Id, token, regularPiece.Id, BuildSegment(new Position(0, 2))),
            CancellationToken.None);

        Assert.Equal(new Position(0, 2), regularPiece.Position);
    }

    [Fact]
    public async Task RegularPiece_CollectsCoin_DuringSlideOnIcePatch()
    {
        // Arrange: the coin sits on the slide destination.
        var (game, p1, _, regularPiece) = GameInMovePhaseWithRegularPiece();
        game.Board.PlaceIcePatch(new Position(0, 2));

        var coinPos = new Position(0, 3);
        game.Board.GetTile(coinPos).SetOccupant(new Coin(CoinType.Silver));

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        var scoreBeforeMove = game.Scores[p1];

        // Act.
        await handler.Handle(
            new MovePieceCommand(game.Id, token, regularPiece.Id, BuildSegment(new Position(0, 2))),
            CancellationToken.None);

        Assert.Null(game.Board.GetTile(coinPos).AsCoin);
        Assert.True(game.Scores[p1] > scoreBeforeMove);
    }

    [Fact]
    public async Task JumpPiece_IgnoresIcePatches_NoSlideOccurs()
    {
        // Arrange: Jump lands on an ice patch on the top edge.
        var (game, p1, _, jumpPiece) = GameInMovePhaseWithJumpPiece();
        game.Board.PlaceIcePatch(new Position(0, 1));

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act.
        await handler.Handle(
            new MovePieceCommand(game.Id, token, jumpPiece.Id, BuildSegment(new Position(0, 1))),
            CancellationToken.None);

        Assert.Equal(new Position(0, 1), jumpPiece.Position);
    }

    [Fact]
    public async Task ChargePiece_SlidesOnIcePatch_AfterChargeMovement()
    {
        // Arrange: the charge ends on an iced board-edge tile.
        var (game, p1, _, chargePiece) = GameInMovePhaseWithChargePiece();
        game.Board.PlaceIcePatch(new Position(0, 7));

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act: charge across the top row.
        await handler.Handle(
            new MovePieceCommand(game.Id, token, chargePiece.Id, BuildSegment(new Position(0, 1))),
            CancellationToken.None);

        Assert.Equal(new Position(0, 7), chargePiece.Position);
    }

    [Fact]
    public async Task EtherealPiece_SlidesOnIcePatch_AfterEtherealMovement()
    {
        // Arrange: Ethereal lands on ice and slides one more tile.
        var (game, p1, _, etherealPiece) = GameInMovePhaseWithEtherealPiece();
        game.Board.PlaceIcePatch(new Position(0, 1));

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act.
        await handler.Handle(
            new MovePieceCommand(game.Id, token, etherealPiece.Id, BuildSegment(new Position(0, 1))),
            CancellationToken.None);

        Assert.Equal(new Position(0, 2), etherealPiece.Position);
    }

    [Fact]
    public async Task IcePatches_PersistAfterGameSaved()
    {
        // Arrange.
        var (game, p1, _, elsaPiece, _) = GameInMovePhaseWithElsa();
        var gameRepo = MockGameRepository(game);

        var token = Guid.NewGuid();
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act.
        await handler.Handle(
            new MovePieceCommand(game.Id, token, elsaPiece.Id,
                BuildMultiSegment(new Position(0, 1), new Position(0, 2), new Position(0, 3))),
            CancellationToken.None);

        var patchCount = game.Board.GetIcePatches().Count();

        Assert.True(game.Board.GetIcePatches().Count() > 0);
        Assert.Equal(patchCount, game.Board.GetIcePatches().Count());
    }

    [Fact]
    public async Task GameState_Persistence_SavesIcePatchesToRepository()
    {
        // Arrange.
        var (game, p1, _, elsaPiece, _) = GameInMovePhaseWithElsa();
        var gameRepo = MockGameRepository(game);

        var token = Guid.NewGuid();
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act.
        await handler.Handle(
            new MovePieceCommand(game.Id, token, elsaPiece.Id,
                BuildMultiSegment(new Position(0, 1), new Position(0, 2), new Position(0, 3))),
            CancellationToken.None);

        await gameRepo.Received(1).SaveAsync(Arg.Is<Game>(g =>
            g.Board.GetIcePatches().Any()), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegularPiece_WithGoldCoin_CollectedDuringSlide()
    {
        // Arrange: gold coin sits on the slide destination.
        var (game, p1, _, regularPiece) = GameInMovePhaseWithRegularPiece();
        game.Board.PlaceIcePatch(new Position(0, 2));
        game.Board.GetTile(new Position(0, 3)).SetOccupant(new Coin(CoinType.Gold));

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        var scoreBeforeMove = game.Scores[p1];

        // Act.
        await handler.Handle(
            new MovePieceCommand(game.Id, token, regularPiece.Id, BuildSegment(new Position(0, 2))),
            CancellationToken.None);

        Assert.Null(game.Board.GetTile(new Position(0, 3)).AsCoin);
        Assert.True(game.Scores[p1] > scoreBeforeMove + 1);
    }

    [Fact]
    public async Task MultiSegmentPath_ElsaPlacesPatches_OnAllIntermediateTiles()
    {
        // Arrange: Elsa follows a longer orthogonal path that turns once.
        var (game, p1, _, elsaPiece, _) = GameInMovePhaseWithElsa();

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act.
        await handler.Handle(
            new MovePieceCommand(game.Id, token, elsaPiece.Id,
                BuildMultiSegment(
                    new Position(0, 1),
                    new Position(0, 2),
                    new Position(1, 2),
                    new Position(2, 2))),
            CancellationToken.None);

        Assert.True(game.Board.HasIcePatch(new Position(0, 1)));
        Assert.True(game.Board.HasIcePatch(new Position(0, 2)));
        Assert.True(game.Board.HasIcePatch(new Position(1, 2)));
    }

    [Fact]
    public async Task IcePatchSliding_Idempotent_MultiplePatchesOnSameTile()
    {
        // Arrange: placing the same patch twice should not change behavior.
        var (game, p1, _, regularPiece) = GameInMovePhaseWithRegularPiece();

        game.Board.PlaceIcePatch(new Position(0, 2));
        game.Board.PlaceIcePatch(new Position(0, 2));

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act.
        await handler.Handle(
            new MovePieceCommand(game.Id, token, regularPiece.Id, BuildSegment(new Position(0, 2))),
            CancellationToken.None);

        Assert.Equal(new Position(0, 3), regularPiece.Position);
    }
}
