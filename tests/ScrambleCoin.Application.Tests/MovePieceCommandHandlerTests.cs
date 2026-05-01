using Microsoft.Extensions.Logging;
using NSubstitute;
using ScrambleCoin.Application.Games.MovePiece;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Services;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Unit tests for <see cref="MovePieceCommandHandler"/> (Issue #11).
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

    // ── Test 1: Handler delegates to domain and saves ──────────────────────────

    [Fact]
    public async Task Handle_ValidCommand_CallsMovePieceAndSavesGame()
    {
        // Arrange
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithPieces();

        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var logger = Substitute.For<ILogger<MovePieceCommandHandler>>();
        var coinSpawnService = new CoinSpawnService(repo, new Random(42), Substitute.For<ILogger<CoinSpawnService>>());
        var handler = new MovePieceCommandHandler(repo, coinSpawnService, logger);

        // Build command: move P1's piece one step right to (0,4)
        var segments = (IReadOnlyList<IReadOnlyList<Position>>)new List<IReadOnlyList<Position>>
        {
            new List<Position> { new Position(0, 4) }.AsReadOnly()
        }.AsReadOnly();

        var command = new MovePieceCommand(game.Id, p1, p1Piece.Id, segments);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: domain applied the move
        Assert.Equal(new Position(0, 4), p1Piece.Position);

        // Assert: game was persisted
        await repo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidCommand_DoesNotThrow()
    {
        // Arrange
        var (game, p1, _, p1Piece, _) = GameInMovePhaseWithPieces();

        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var logger = Substitute.For<ILogger<MovePieceCommandHandler>>();
        var coinSpawnService = new CoinSpawnService(repo, new Random(42), Substitute.For<ILogger<CoinSpawnService>>());
        var handler = new MovePieceCommandHandler(repo, coinSpawnService, logger);

        var segments = (IReadOnlyList<IReadOnlyList<Position>>)new List<IReadOnlyList<Position>>
        {
            new List<Position> { new Position(0, 4) }.AsReadOnly()
        }.AsReadOnly();

        var command = new MovePieceCommand(game.Id, p1, p1Piece.Id, segments);

        // Act & Assert: no exception
        var ex = await Record.ExceptionAsync(() => handler.Handle(command, CancellationToken.None));
        Assert.Null(ex);
    }

    // ── Test 2: Handler propagates domain exceptions ───────────────────────────

    [Fact]
    public async Task Handle_WhenDomainThrows_ExceptionPropagates()
    {
        // Arrange: the game is in CoinSpawn phase — MovePiece will throw DomainException
        var (game, p1) = GameInCoinSpawnPhase();

        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var logger = Substitute.For<ILogger<MovePieceCommandHandler>>();
        var coinSpawnService = new CoinSpawnService(repo, new Random(42), Substitute.For<ILogger<CoinSpawnService>>());
        var handler = new MovePieceCommandHandler(repo, coinSpawnService, logger);

        var command = new MovePieceCommand(game.Id, p1, Guid.NewGuid(), new List<IReadOnlyList<Position>>());

        // Act & Assert: DomainException propagates through the handler
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenDomainThrows_GameIsNotSaved()
    {
        // Arrange: game in the wrong phase → domain throws before SaveAsync
        var (game, p1) = GameInCoinSpawnPhase();

        var repo = Substitute.For<IGameRepository>();
        repo.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var logger = Substitute.For<ILogger<MovePieceCommandHandler>>();
        var coinSpawnService = new CoinSpawnService(repo, new Random(42), Substitute.For<ILogger<CoinSpawnService>>());
        var handler = new MovePieceCommandHandler(repo, coinSpawnService, logger);

        var command = new MovePieceCommand(game.Id, p1, Guid.NewGuid(), new List<IReadOnlyList<Position>>());

        // Act: swallow the exception
        try { await handler.Handle(command, CancellationToken.None); } catch { /* expected */ }

        // Assert: SaveAsync was never called
        await repo.DidNotReceive().SaveAsync(Arg.Any<Game>(), Arg.Any<CancellationToken>());
    }
}
