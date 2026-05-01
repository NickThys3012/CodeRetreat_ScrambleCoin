using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ScrambleCoin.Application.Games.MovePiece;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Notifications;
using ScrambleCoin.Application.Services;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.ValueObjects;
namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Unit tests for <see cref="MovePieceCommandHandler"/> (Issue #11 / #36).
/// </summary>
public class MovePieceCommandHandlerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a game in MovePhase with one piece per player on the board.
    /// P1's piece is at (0,3); P2's piece is at (7,3).
    /// </summary>
    private static (Game game, Guid p1, Guid p2, Piece p1Piece, Piece p2Piece)
        GameInMovePhaseWithPieces()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var p1Piece = new Piece(Guid.NewGuid(), "P1Mover", p1,
            EntryPointType.Borders, MovementType.Orthogonal, 3, 1);
        var p1Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"P1Fill{i}", p1, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var p2Piece = new Piece(Guid.NewGuid(), "P2Mover", p2,
            EntryPointType.Borders, MovementType.Orthogonal, 1, 1);
        var p2Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"P2Fill{i}", p2, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(new[] { p1Piece }.Concat(p1Fill)));
        game.SetLineup(p2, new Lineup(new[] { p2Piece }.Concat(p2Fill)));
        game.Start();
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        // Placing both pieces auto-advances to MovePhase.
        game.PlacePiece(p1, p1Piece.Id, new Position(0, 3));
        game.PlacePiece(p2, p2Piece.Id, new Position(7, 3));

        return (game, p1, p2, p1Piece, p2Piece);
    }

    /// <summary>
    /// Creates a game in CoinSpawn phase (right after Start).
    /// </summary>
    private static (Game game, Guid p1) GameInCoinSpawnPhase()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var p1Pieces = Enumerable.Range(0, 5)
            .Select(i => new Piece(Guid.NewGuid(), $"P1Piece{i}", p1, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();
        var p2Pieces = Enumerable.Range(0, 5)
            .Select(i => new Piece(Guid.NewGuid(), $"P2Piece{i}", p2, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(p1Pieces));
        game.SetLineup(p2, new Lineup(p2Pieces));
        game.Start(); // → CoinSpawn

        return (game, p1);
    }

    private static MovePieceCommandHandler BuildHandler(IGameRepository repo, IPublisher? publisher = null)
        => new(repo, publisher ?? Substitute.For<IPublisher>(),
               Substitute.For<ILogger<MovePieceCommandHandler>>());

    // ── Test 1: Handler delegates to domain and saves ──────────────────────────

    [Fact]
    public async Task Handle_ValidCommand_CallsMovePieceAndSavesGame()
    {
        // Arrange
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithPieces();

        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var handler = BuildHandler(repo);

        var segments = (IReadOnlyList<IReadOnlyList<Position>>)new List<IReadOnlyList<Position>>
        {
            new List<Position> { new Position(0, 4) }.AsReadOnly()
        }.AsReadOnly();

        // Act
        await handler.Handle(new MovePieceCommand(game.Id, p1, p1Piece.Id, segments), CancellationToken.None);

        // Assert: domain applied the move
        Assert.Equal(new Position(0, 4), p1Piece.Position);

        // Assert: game was persisted
        await repo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidCommand_DoesNotThrow()
    {
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithPieces();

        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var segments = (IReadOnlyList<IReadOnlyList<Position>>)new List<IReadOnlyList<Position>>
        {
            new List<Position> { new Position(0, 4) }.AsReadOnly()
        }.AsReadOnly();

        var ex = await Record.ExceptionAsync(() =>
            BuildHandler(repo).Handle(new MovePieceCommand(game.Id, p1, p1Piece.Id, segments), CancellationToken.None));

        Assert.Null(ex);
    }

    // ── Test 2: Handler propagates domain exceptions ───────────────────────────

    [Fact]
    public async Task Handle_WhenDomainThrows_ExceptionPropagates()
    {
        var (game, p1) = GameInCoinSpawnPhase();

        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var command = new MovePieceCommand(game.Id, p1, Guid.NewGuid(), new List<IReadOnlyList<Position>>());

        await Assert.ThrowsAsync<DomainException>(() =>
            BuildHandler(repo).Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenDomainThrows_GameIsNotSaved()
    {
        var (game, p1) = GameInCoinSpawnPhase();

        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var command = new MovePieceCommand(game.Id, p1, Guid.NewGuid(), new List<IReadOnlyList<Position>>());

        try { await BuildHandler(repo).Handle(command, CancellationToken.None); } catch { /* expected */ }

        await repo.DidNotReceive().SaveAsync(Arg.Any<Game>(), Arg.Any<CancellationToken>());
    }

    // ── Test 3: Turn rollover publishes TurnRolledOver notification ────────────

    [Fact]
    public async Task Handle_WhenMoveCompletesNewTurn_PublishesTurnRolledOver()
    {
        // Arrange: P1 has already moved; P2's move will complete the turn.
        var (game, p1, p2, p1Piece, p2Piece) = GameInMovePhaseWithPieces();

        var p1Segments = (IReadOnlyList<IReadOnlyList<Position>>)new List<IReadOnlyList<Position>>
        {
            new List<Position> { new Position(0, 4) }.AsReadOnly()
        }.AsReadOnly();
        game.MovePiece(p1, p1Piece.Id, p1Segments); // → MovePhaseActivePlayer = P2

        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var publisher = Substitute.For<IPublisher>();
        var handler = BuildHandler(repo, publisher);

        var p2Segments = (IReadOnlyList<IReadOnlyList<Position>>)new List<IReadOnlyList<Position>>
        {
            new List<Position> { new Position(7, 2) }.AsReadOnly()
        }.AsReadOnly();

        // Act
        await handler.Handle(new MovePieceCommand(game.Id, p2, p2Piece.Id, p2Segments), CancellationToken.None);

        // Assert: TurnRolledOver notification published for the correct game
        await publisher.Received(1).Publish(
            Arg.Is<TurnRolledOver>(n => n.GameId == game.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenMoveDoesNotCompleteATurn_DoesNotPublishTurnRolledOver()
    {
        // Arrange: only P1 moves — turn is not yet complete.
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithPieces();

        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var publisher = Substitute.For<IPublisher>();
        var handler = BuildHandler(repo, publisher);

        var segments = (IReadOnlyList<IReadOnlyList<Position>>)new List<IReadOnlyList<Position>>
        {
            new List<Position> { new Position(0, 4) }.AsReadOnly()
        }.AsReadOnly();

        // Act
        await handler.Handle(new MovePieceCommand(game.Id, p1, p1Piece.Id, segments), CancellationToken.None);

        // Assert: no TurnRolledOver published
        await publisher.DidNotReceive().Publish(Arg.Any<TurnRolledOver>(), Arg.Any<CancellationToken>());
    }
}

/// <summary>
/// Unit tests for <see cref="TurnRolledOverHandler"/>.
/// </summary>
public class TurnRolledOverHandlerTests
{
    [Fact]
    public async Task Handle_CallsCoinSpawnServiceWithCorrectGameId()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var coinSpawnService = Substitute.For<ICoinSpawnService>();

        var handler = new TurnRolledOverHandler(
            coinSpawnService,
            Substitute.For<ILogger<TurnRolledOverHandler>>());

        // Act
        await handler.Handle(new TurnRolledOver(gameId), CancellationToken.None);

        // Assert
        await coinSpawnService.Received(1).ExecuteAsync(gameId, Arg.Any<CancellationToken>());
    }
}
