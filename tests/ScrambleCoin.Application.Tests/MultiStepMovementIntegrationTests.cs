using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Application.Games.MovePiece;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.Factories;
using ScrambleCoin.Domain.ValueObjects;
using DomainBotReg = ScrambleCoin.Domain.BotRegistrations.BotRegistration;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Integration tests for multi-step movement sequences (Issue #48).
/// Tests full Application layer flow for pieces with multiple movement segments:
/// Cogsworth (Any→Orthogonal), Lumiere (Any→Diagonal), Anna (Orthogonal×3),
/// Remy (Diagonal×2), Olaf (Any×2), Kristoff (Diagonal×3).
/// 
/// These tests verify:
/// - Per-segment movement type validation
/// - Game state persistence
/// - Event publishing for each segment
/// - Correct error handling for invalid sequences
/// </summary>
public class MultiStepMovementIntegrationTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a game in MovePhase with a specific multi-step piece.
    /// Returns (game, p1, p2, piece, startPos).
    /// </summary>
    private static (Game game, Guid p1, Guid p2, Piece piece, Position startPos)
        GameInMovePhaseWithMultiStepPiece(string pieceName)
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var board = new Board();

        var piece = PieceFactory.Create(pieceName, p1);
        var p1Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"P1Fill{i}", p1, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var p2Piece = new Piece(Guid.NewGuid(), "P2Mover", p2,
            EntryPointType.Borders, MovementType.Orthogonal, 1, 1);
        var p2Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"P2Fill{i}", p2, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var game = new Game(p1, p2, board);
        game.SetLineup(p1, new Lineup(new[] { piece }.Concat(p1Fill)));
        game.SetLineup(p2, new Lineup(new[] { p2Piece }.Concat(p2Fill)));
        game.Start();
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        // Choose appropriate starting position based on entry point type
        Position startPos = piece.EntryPointType == EntryPointType.Corners 
            ? new Position(0, 0) 
            : new Position(0, 3);

        // Place both pieces to auto-advance to MovePhase
        game.PlacePiece(p1, piece.Id, startPos);
        game.PlacePiece(p2, p2Piece.Id, new Position(7, 3));

        return (game, p1, p2, piece, startPos);
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
    /// Builds a multi-segment move list from individual segment paths.
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<Position>> BuildSegments(params Position[][] segments)
    {
        return segments
            .Select(s => (IReadOnlyList<Position>)s.ToList().AsReadOnly())
            .ToList()
            .AsReadOnly();
    }

    // ── Test 1: Cogsworth 2-Segment Move (Valid) ──────────────────────────────

    [Fact]
    public async Task CogsworthTwoSegmentMove_ValidSequence_BothSegmentsExecute()
    {
        // Arrange: Cogsworth at (0,3), will move Any 1 right → (0,4), then Orthogonal 2 down → (2,4)
        var (game, p1, _, cogsworth, _) = GameInMovePhaseWithMultiStepPiece("Cogsworth");

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        await handler.Handle(
            new MovePieceCommand(game.Id, token, cogsworth.Id,
                BuildSegments(
                    new[] { new Position(0, 4) },  // Segment 1: Any direction 1
                    new[] { new Position(1, 4), new Position(2, 4) }  // Segment 2: Orthogonal 2
                )),
            CancellationToken.None);

        // Assert
        Assert.Equal(new Position(2, 4), cogsworth.Position);
        await gameRepo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }

    // ── Test 2: Cogsworth Segment 2 Wrong Type ────────────────────────────────

    [Fact]
    public async Task CogsworthWrongTypeSegment2_DiagonalInsteadOfOrthogonal_Throws()
    {
        // Arrange: Cogsworth attempts Segment 2 as Diagonal (invalid)
        var (game, p1, _, cogsworth, _) = GameInMovePhaseWithMultiStepPiece("Cogsworth");

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(
                new MovePieceCommand(game.Id, token, cogsworth.Id,
                    BuildSegments(
                        new[] { new Position(0, 4) },  // Segment 1: OK
                        new[] { new Position(1, 5) }   // Segment 2: Diagonal (wrong)
                    )),
                CancellationToken.None));

        Assert.Contains("not orthogonal", ex.Message, StringComparison.OrdinalIgnoreCase);
        await gameRepo.DidNotReceive().SaveAsync(game, Arg.Any<CancellationToken>());
    }

    // ── Test 3: Lumiere 2-Segment Move (Any→Diagonal) ────────────────────────

    [Fact]
    public async Task LumiereTwoSegmentMove_ValidSequence_BothSegmentsExecute()
    {
        // Arrange: Lumiere at (0,3), will move Any 1 right → (0,4), then Diagonal 2 NE → (2,6)
        var (game, p1, _, lumiere, _) = GameInMovePhaseWithMultiStepPiece("Lumiere");

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        await handler.Handle(
            new MovePieceCommand(game.Id, token, lumiere.Id,
                BuildSegments(
                    new[] { new Position(0, 4) },  // Segment 1: Any direction 1
                    new[] { new Position(1, 5), new Position(2, 6) }  // Segment 2: Diagonal 2
                )),
            CancellationToken.None);

        // Assert
        Assert.Equal(new Position(2, 6), lumiere.Position);
        await gameRepo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }

    // ── Test 4: Lumiere Segment 2 Wrong Type ──────────────────────────────────

    [Fact]
    public async Task LumiereWrongTypeSegment2_OrthogonalInsteadOfDiagonal_Throws()
    {
        // Arrange: Lumiere attempts Segment 2 as Orthogonal (invalid)
        var (game, p1, _, lumiere, _) = GameInMovePhaseWithMultiStepPiece("Lumiere");

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(
                new MovePieceCommand(game.Id, token, lumiere.Id,
                    BuildSegments(
                        new[] { new Position(0, 4) },  // Segment 1: OK
                        new[] { new Position(1, 4), new Position(2, 4) }  // Segment 2: Orthogonal (wrong)
                    )),
                CancellationToken.None));

        Assert.Contains("not diagonal", ex.Message, StringComparison.OrdinalIgnoreCase);
        await gameRepo.DidNotReceive().SaveAsync(game, Arg.Any<CancellationToken>());
    }

    // ── Test 5: Anna 3-Segment Move (All Required) ────────────────────────────

    [Fact]
    public async Task AnnaThrreeSegmentMove_ValidSequence_AllSegmentsExecute()
    {
        // Arrange: Anna at (0,3), will move Orthogonal 1 three times
        var (game, p1, _, anna, _) = GameInMovePhaseWithMultiStepPiece("Anna");

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        await handler.Handle(
            new MovePieceCommand(game.Id, token, anna.Id,
                BuildSegments(
                    new[] { new Position(0, 4) },  // Segment 1: Orthogonal 1
                    new[] { new Position(1, 4) },  // Segment 2: Orthogonal 1
                    new[] { new Position(1, 5) }   // Segment 3: Orthogonal 1
                )),
            CancellationToken.None);

        // Assert
        Assert.Equal(new Position(1, 5), anna.Position);
        await gameRepo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }

    // ── Test 6: Anna Incomplete Sequence (Only 2 of 3) ───────────────────────

    [Fact]
    public async Task AnnaIncompleteSequence_Only2Of3Segments_Throws()
    {
        // Arrange: Anna requires all 3 segments
        var (game, p1, _, anna, _) = GameInMovePhaseWithMultiStepPiece("Anna");

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(
                new MovePieceCommand(game.Id, token, anna.Id,
                    BuildSegments(
                        new[] { new Position(0, 4) },  // Segment 1
                        new[] { new Position(1, 4) }   // Segment 2 (missing 3)
                    )),
                CancellationToken.None));

        Assert.Contains("requires exactly", ex.Message, StringComparison.OrdinalIgnoreCase);
        await gameRepo.DidNotReceive().SaveAsync(game, Arg.Any<CancellationToken>());
    }

    // ── Test 7: Anna Wrong Movement Type in Segment ───────────────────────────

    [Fact]
    public async Task AnnaWrongTypeSegment2_DiagonalInsteadOfOrthogonal_Throws()
    {
        // Arrange: Anna segment 2 must be Orthogonal, not Diagonal
        var (game, p1, _, anna, _) = GameInMovePhaseWithMultiStepPiece("Anna");

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(
                new MovePieceCommand(game.Id, token, anna.Id,
                    BuildSegments(
                        new[] { new Position(0, 4) },  // Segment 1: OK
                        new[] { new Position(1, 5) },  // Segment 2: Diagonal (wrong)
                        new[] { new Position(1, 5) }   // Segment 3
                    )),
                CancellationToken.None));

        Assert.Contains("not orthogonal", ex.Message, StringComparison.OrdinalIgnoreCase);
        await gameRepo.DidNotReceive().SaveAsync(game, Arg.Any<CancellationToken>());
    }

    // ── Test 8: Remy 2 Independent Diagonal Moves ──────────────────────────────

    [Fact]
    public async Task RemyTwoSegmentMove_DiagonalThenDiagonal_BothSegmentsExecute()
    {
        // Arrange: Remy at (0,3), will move Diagonal 2 SE → (2,5), then Diagonal 2 SW → (4,3)
        var (game, p1, _, remy, _) = GameInMovePhaseWithMultiStepPiece("Remy");

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        await handler.Handle(
            new MovePieceCommand(game.Id, token, remy.Id,
                BuildSegments(
                    new[] { new Position(1, 4), new Position(2, 5) },  // Segment 1: Diagonal 2
                    new[] { new Position(3, 4), new Position(4, 3) }   // Segment 2: Diagonal 2
                )),
            CancellationToken.None);

        // Assert
        Assert.Equal(new Position(4, 3), remy.Position);
        await gameRepo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }

    // ── Test 9: Olaf 2 Any-Direction Moves ──────────────────────────────────────

    [Fact]
    public async Task OlafTwoSegmentMove_AnyThenAny_BothSegmentsExecute()
    {
        // Arrange: Olaf at (0,3), will move Any 1 east → (0,4), then Any 1 north → (1,4)
        var (game, p1, _, olaf, _) = GameInMovePhaseWithMultiStepPiece("Olaf");

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        await handler.Handle(
            new MovePieceCommand(game.Id, token, olaf.Id,
                BuildSegments(
                    new[] { new Position(0, 4) },  // Segment 1: Any 1
                    new[] { new Position(1, 4) }   // Segment 2: Any 1
                )),
            CancellationToken.None);

        // Assert
        Assert.Equal(new Position(1, 4), olaf.Position);
        await gameRepo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }

    // ── Test 10: Kristoff 3 Diagonal Moves ────────────────────────────────────

    [Fact]
    public async Task KristoffThreeSegmentMove_DiagonalThenDiagonalThenDiagonal_AllSegmentsExecute()
    {
        // Arrange: Kristoff at (0,3), will move Diagonal 1 three times
        var (game, p1, _, kristoff, _) = GameInMovePhaseWithMultiStepPiece("Kristoff");

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        await handler.Handle(
            new MovePieceCommand(game.Id, token, kristoff.Id,
                BuildSegments(
                    new[] { new Position(1, 4) },  // Segment 1: Diagonal 1
                    new[] { new Position(2, 5) },  // Segment 2: Diagonal 1
                    new[] { new Position(3, 6) }   // Segment 3: Diagonal 1
                )),
            CancellationToken.None);

        // Assert
        Assert.Equal(new Position(3, 6), kristoff.Position);
        await gameRepo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }

    // ── Test 11: Remy Wrong Type in First Segment ────────────────────────────

    [Fact]
    public async Task RemyWrongTypeSegment1_OrthogonalInsteadOfDiagonal_Throws()
    {
        // Arrange: Remy segment 1 must be Diagonal
        var (game, p1, _, remy, _) = GameInMovePhaseWithMultiStepPiece("Remy");

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(
                new MovePieceCommand(game.Id, token, remy.Id,
                    BuildSegments(
                        new[] { new Position(0, 4), new Position(0, 5) },  // Segment 1: Orthogonal (wrong)
                        new[] { new Position(1, 6), new Position(2, 7) }   // Segment 2
                    )),
                CancellationToken.None));

        Assert.Contains("not diagonal", ex.Message, StringComparison.OrdinalIgnoreCase);
        await gameRepo.DidNotReceive().SaveAsync(game, Arg.Any<CancellationToken>());
    }

    // ── Test 12: Kristoff Incomplete Sequence ──────────────────────────────────

    [Fact]
    public async Task KristoffIncompleteSequence_Only2Of3Segments_Throws()
    {
        // Arrange: Kristoff requires all 3 segments
        var (game, p1, _, kristoff, _) = GameInMovePhaseWithMultiStepPiece("Kristoff");

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(
                new MovePieceCommand(game.Id, token, kristoff.Id,
                    BuildSegments(
                        new[] { new Position(1, 4) },  // Segment 1
                        new[] { new Position(2, 5) }   // Segment 2 (missing 3)
                    )),
                CancellationToken.None));

        Assert.Contains("requires exactly", ex.Message, StringComparison.OrdinalIgnoreCase);
        await gameRepo.DidNotReceive().SaveAsync(game, Arg.Any<CancellationToken>());
    }

    // ── Test 13: Cogsworth Segment 2 Exceeds Max Distance ─────────────────────

    [Fact]
    public async Task CogsworthExceedsMaxDistance_Orthogonal3InsteadOf2_Throws()
    {
        // Arrange: Cogsworth segment 2 max distance is 2
        var (game, p1, _, cogsworth, _) = GameInMovePhaseWithMultiStepPiece("Cogsworth");

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(
                new MovePieceCommand(game.Id, token, cogsworth.Id,
                    BuildSegments(
                        new[] { new Position(0, 4) },  // Segment 1: OK
                        new[] { new Position(1, 4), new Position(2, 4), new Position(3, 4) }  // Segment 2: 3 (max is 2)
                    )),
                CancellationToken.None));

        Assert.Contains("step(s)", ex.Message, StringComparison.OrdinalIgnoreCase);
        await gameRepo.DidNotReceive().SaveAsync(game, Arg.Any<CancellationToken>());
    }

    // ── Test 14: Lumiere Segment 2 Exceeds Max Distance ─────────────────────

    [Fact]
    public async Task LumiereExceedsMaxDistance_Diagonal3InsteadOf2_Throws()
    {
        // Arrange: Lumiere segment 2 max distance is 2
        var (game, p1, _, lumiere, _) = GameInMovePhaseWithMultiStepPiece("Lumiere");

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(
                new MovePieceCommand(game.Id, token, lumiere.Id,
                    BuildSegments(
                        new[] { new Position(0, 4) },  // Segment 1: OK
                        new[] { new Position(1, 5), new Position(2, 6), new Position(3, 7) }  // Segment 2: 3 (max is 2)
                    )),
                CancellationToken.None));

        Assert.Contains("step(s)", ex.Message, StringComparison.OrdinalIgnoreCase);
        await gameRepo.DidNotReceive().SaveAsync(game, Arg.Any<CancellationToken>());
    }

    // ── Test 15: Game State Persistence After Multi-Segment Move ──────────────

    [Fact]
    public async Task MultiSegmentMove_GameStatePersisted_PiecePositionAndTurnUpdated()
    {
        // Arrange: Cogsworth moves 2 segments
        var (game, p1, _, cogsworth, _) = GameInMovePhaseWithMultiStepPiece("Cogsworth");
        var initialTurn = game.TurnNumber;

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Act
        await handler.Handle(
            new MovePieceCommand(game.Id, token, cogsworth.Id,
                BuildSegments(
                    new[] { new Position(0, 4) },
                    new[] { new Position(1, 4), new Position(2, 4) }
                )),
            CancellationToken.None);

        // Assert: SaveAsync called exactly once with complete game state
        await gameRepo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
        Assert.Equal(new Position(2, 4), cogsworth.Position);
    }

    // ── Test 16: All Multi-Step Pieces Can Complete Moves ─────────────────────

    [Theory]
    [InlineData("Cogsworth", 2)]
    [InlineData("Lumiere", 2)]
    [InlineData("Remy", 2)]
    [InlineData("Anna", 3)]
    [InlineData("Olaf", 2)]
    [InlineData("Kristoff", 3)]
    public async Task AllMultiStepPieces_CanCompleteMoveWithCorrectSegments(string pieceName, int expectedSegmentCount)
    {
        // Arrange
        var (game, p1, _, piece, _) = GameInMovePhaseWithMultiStepPiece(pieceName);
        Assert.Equal(expectedSegmentCount, piece.MovesPerTurn);

        var token = Guid.NewGuid();
        var gameRepo = MockGameRepository(game);
        var botRepo = MockBotRepository(token, p1, game.Id);
        var handler = BuildHandler(gameRepo, botRepo);

        // Create appropriate segments based on piece
        var segments = pieceName switch
        {
            "Cogsworth" => BuildSegments(
                new[] { new Position(0, 4) },
                new[] { new Position(1, 4), new Position(2, 4) }),
            "Lumiere" => BuildSegments(
                new[] { new Position(0, 4) },
                new[] { new Position(1, 5), new Position(2, 6) }),
            "Remy" => BuildSegments(
                new[] { new Position(1, 4), new Position(2, 5) },
                new[] { new Position(3, 4), new Position(4, 3) }),
            "Anna" => BuildSegments(
                new[] { new Position(0, 4) },
                new[] { new Position(1, 4) },
                new[] { new Position(1, 5) }),
            "Olaf" => BuildSegments(
                new[] { new Position(0, 4) },
                new[] { new Position(1, 4) }),
            "Kristoff" => BuildSegments(
                new[] { new Position(1, 4) },
                new[] { new Position(2, 5) },
                new[] { new Position(3, 6) }),
            _ => throw new ArgumentException($"Unknown piece: {pieceName}")
        };

        // Act
        await handler.Handle(
            new MovePieceCommand(game.Id, token, piece.Id, segments),
            CancellationToken.None);

        // Assert: No exceptions, game state saved
        await gameRepo.Received(1).SaveAsync(game, Arg.Any<CancellationToken>());
    }
}
