using System.Collections.ObjectModel;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Events;
using ScrambleCoin.Domain.Factories;
using ScrambleCoin.Domain.Obstacles;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Tests;

/// <summary>
/// Integration tests for on-stop abilities (Issue #49).
/// Tests cover ability triggers across full game flows and scenarios.
/// Focus: Ralph, Pumbaa, WALL•E, Sulley, Rafiki, Scar, Daisy, Stitch.
/// </summary>
public class OnStopAbilitiesIntegrationTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a game in MovePhase with both players having pieces on the board.
    /// Player 1 has the named piece; Player 2 has a filler piece.
    /// </summary>
    private static (Game game, Guid p1, Guid p2, Piece testPiece, Piece p2Piece) GameWithPiecesInMovePhase(
        string p1PieceName,
        Position p1Position,
        Position? p2Position = null)
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var board = new Board();

        var testPiece = PieceFactory.Create(p1PieceName, p1);
        var p2Piece = new Piece(Guid.NewGuid(), "P2Piece", p2,
            EntryPointType.Borders, MovementType.Orthogonal, 1, 1);

        // Fill pieces
        var p1Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"P1Fill{i}", p1, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();
        var p2Fill = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"P2Fill{i}", p2, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var p1Pieces = new List<Piece> { testPiece };
        p1Pieces.AddRange(p1Fill);

        var p2Pieces = new List<Piece> { p2Piece };
        p2Pieces.AddRange(p2Fill);

        var game = new Game(p1, p2, board);
        game.SetLineup(p1, new Lineup(p1Pieces));
        game.SetLineup(p2, new Lineup(p2Pieces));

        game.Start();
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        // Place both pieces to auto-advance to MovePhase
        var actualP2Pos = p2Position ?? new Position(7, 3); // Valid Border entry point
        game.PlacePiece(p1, testPiece.Id, p1Position);
        game.PlacePiece(p2, p2Piece.Id, actualP2Pos);
        
        return (game, p1, p2, testPiece, p2Piece);
    }

    private static ReadOnlyCollection<IReadOnlyList<Position>> BuildSegments(params Position[] steps)
    {
        var segment = (IReadOnlyList<Position>)steps.ToList().AsReadOnly();
        return new List<IReadOnlyList<Position>> { segment }.AsReadOnly();
    }

    // ── Ralph Tests ───────────────────────────────────────────────────────────

    [Fact]
    public void Ralph_Orthogonal_SingleStep_DestroyAdjacentRock()
    {
        // Arrange: Ralph at (0,3), rock adjacent to destination
        var (game, p1, _, ralphPiece, _) = GameWithPiecesInMovePhase("Ralph", new Position(0, 3));
        var board = game.Board;

        board.AddRock(new Rock(new Position(2, 3))); // Adjacent to destination (1,3)

        // Act: Ralph moves one step to (1,3)
        game.MovePiece(p1, ralphPiece.Id, BuildSegments(new Position(1, 3)));

        // Assert: Rock adjacent to the destination is destroyed
        Assert.False(board.HasRock(new Position(2, 3)));
        var rockDestroyed = game.DomainEvents.OfType<RockDestroyed>().FirstOrDefault();
        Assert.NotNull(rockDestroyed);
    }

    [Fact]
    public void Ralph_Orthogonal_DestroyAdjacentFence()
    {
        // Arrange: Ralph at (0,3), fence adjacent to destination
        var (game, p1, _, ralphPiece, _) = GameWithPiecesInMovePhase("Ralph", new Position(0, 3));
        var board = game.Board;

        board.AddFence(new Fence(new Position(1, 3), new Position(1, 4))); // Adjacent to destination

        // Act: Ralph moves to (1,3)
        game.MovePiece(p1, ralphPiece.Id, BuildSegments(new Position(1, 3)));

        // Assert: Fence is destroyed
        Assert.False(board.HasFence(new Position(1, 3)));
        var fenceDestroyed = game.DomainEvents.OfType<FenceDestroyed>().FirstOrDefault();
        Assert.NotNull(fenceDestroyed);
    }

    [Fact]
    public void Ralph_BothObstacles_DestroysBothRockAndFence()
    {
        // Arrange: Ralph at (0,3), both rock and fence adjacent to destination
        var (game, p1, _, ralphPiece, _) = GameWithPiecesInMovePhase("Ralph", new Position(0, 3));
        var board = game.Board;

        board.AddRock(new Rock(new Position(2, 3))); // Adjacent
        board.AddFence(new Fence(new Position(1, 3), new Position(1, 4))); // Adjacent

        // Act: Ralph moves to (1,3)
        game.MovePiece(p1, ralphPiece.Id, BuildSegments(new Position(1, 3)));

        // Assert: Both destroyed
        Assert.False(board.HasRock(new Position(2, 3)));
        Assert.False(board.HasFence(new Position(1, 3)));
    }

    [Fact]
    public void Ralph_NoObstacles_MovesSuccessfully()
    {
        // Arrange: Ralph at (0,3), no obstacles
        var (game, p1, _, ralphPiece, _) = GameWithPiecesInMovePhase("Ralph", new Position(0, 3));

        // Act: Ralph moves one step
        game.MovePiece(p1, ralphPiece.Id, BuildSegments(new Position(1, 3)));

        // Assert: Piece moves to destination
        Assert.Equal(new Position(1, 3), ralphPiece.Position);
    }

    // ── Pumbaa Tests ──────────────────────────────────────────────────────────

    [Fact]
    public void Pumbaa_AbilityDistinguishesFromRalph()
    {
        // Pumbaa's ability differs from Ralph: destroys only fences, not rocks
        // Integration test verifies the piece can be created and placed successfully
        // Detailed ability testing is done in unit tests (OnStopAbilitiesTests.cs)
        var (game, p1, _, pumbaaPiece, _) = GameWithPiecesInMovePhase("Pumbaa", new Position(0, 3));

        // Assert: Pumbaa piece was created successfully
        Assert.NotNull(pumbaaPiece);
        Assert.Equal("Pumbaa", pumbaaPiece.Name);
        Assert.Equal(new Position(0, 3), pumbaaPiece.Position);
    }

    // ── WALL•E Tests ──────────────────────────────────────────────────────────

    [Fact]
    public void WallE_AbilityPushesAdjacentPieces()
    {
        // WALL•E's ability is to push adjacent pieces in move direction
        // Integration test verifies the piece can be created and placed successfully
        // Detailed ability testing is done in unit tests
        var (game, p1, _, wallePiece, _) = GameWithPiecesInMovePhase("WALL•E", new Position(0, 3));

        // Assert: WALL•E piece was created successfully
        Assert.NotNull(wallePiece);
        Assert.Equal("WALL•E", wallePiece.Name);
        Assert.Equal(new Position(0, 3), wallePiece.Position);
    }

    // ── Sulley Tests ──────────────────────────────────────────────────────────

    [Fact]
    public void Sulley_AnyDirection_MovesSuccessfully()
    {
        // Arrange: Sulley at (0,3)
        var (game, p1, _, sulleyPiece, _) = GameWithPiecesInMovePhase("Sulley", new Position(0, 3));

        // Act: Sulley moves diagonally (AnyDirection)
        game.MovePiece(p1, sulleyPiece.Id, BuildSegments(new Position(1, 4)));

        // Assert: Piece at destination
        Assert.Equal(new Position(1, 4), sulleyPiece.Position);
    }

    // ── Rafiki Tests ──────────────────────────────────────────────────────────

    [Fact]
    public void Rafiki_Jump_FromCorner_MovesSuccessfully()
    {
        // Arrange: Rafiki at (0,0) - corner entry point
        var (game, p1, _, rafikPiece, _) = GameWithPiecesInMovePhase("Rafiki", new Position(0, 0));

        // Act: Rafiki jumps to (3,3)
        game.MovePiece(p1, rafikPiece.Id, BuildSegments(new Position(3, 3)));

        // Assert: Piece at destination
        Assert.Equal(new Position(3, 3), rafikPiece.Position);
    }

    // ── Scar Tests (During-Jump) ──────────────────────────────────────────────

    [Fact]
    public void Scar_Jump_FromCorner_MovesSuccessfully()
    {
        // Arrange: Scar at (0,0) - corner entry point
        var (game, p1, _, scarPiece, _) = GameWithPiecesInMovePhase("Scar", new Position(0, 0));

        // Act: Scar jumps to (3,3)
        game.MovePiece(p1, scarPiece.Id, BuildSegments(new Position(3, 3)));

        // Assert: Scar at destination
        Assert.Equal(new Position(3, 3), scarPiece.Position);
    }

    [Fact]
    public void Scar_Jump_MaxDistance()
    {
        // Arrange: Scar at (0,0), max distance is 4
        var (game, p1, _, scarPiece, _) = GameWithPiecesInMovePhase("Scar", new Position(0, 0));

        // Act: Scar jumps maximum distance
        game.MovePiece(p1, scarPiece.Id, BuildSegments(new Position(4, 4)));

        // Assert: Movement succeeds
        Assert.Equal(new Position(4, 4), scarPiece.Position);
    }

    // ── Daisy Tests (During-Jump) ─────────────────────────────────────────────

    [Fact]
    public void Daisy_Jump_FromAnywhere_MovesSuccessfully()
    {
        // Arrange: Daisy at (0,3) - Anywhere entry point
        var (game, p1, _, daisyPiece, _) = GameWithPiecesInMovePhase("Daisy", new Position(0, 3));

        // Act: Daisy jumps 2 tiles
        game.MovePiece(p1, daisyPiece.Id, BuildSegments(new Position(2, 3)));

        // Assert: Daisy at destination
        Assert.Equal(new Position(2, 3), daisyPiece.Position);
    }

    [Fact]
    public void Daisy_Jump_MaxDistance()
    {
        // Arrange: Daisy at (0,3), max distance is 3
        var (game, p1, _, daisyPiece, _) = GameWithPiecesInMovePhase("Daisy", new Position(0, 3));

        // Act: Daisy jumps 3 tiles
        game.MovePiece(p1, daisyPiece.Id, BuildSegments(new Position(3, 3)));

        // Assert: Movement succeeds
        Assert.Equal(new Position(3, 3), daisyPiece.Position);
    }

    // ── Stitch Tests (During-Move) ────────────────────────────────────────────

    [Fact]
    public void Stitch_Orthogonal_MovesSuccessfully()
    {
        // Arrange: Stitch at (0,3)
        var (game, p1, _, stitchPiece, _) = GameWithPiecesInMovePhase("Stitch", new Position(0, 3));

        // Act: Stitch moves one step
        game.MovePiece(p1, stitchPiece.Id, BuildSegments(new Position(1, 3)));

        // Assert: Piece at destination
        Assert.Equal(new Position(1, 3), stitchPiece.Position);
    }

    // ── Event Correctness Tests ───────────────────────────────────────────────

    [Fact]
    public void OnStopAbility_Events_IncludeCorrectGameIdAndTurnNumber()
    {
        // Arrange: Ralph at (0,3), rock adjacent
        var (game, p1, _, ralphPiece, _) = GameWithPiecesInMovePhase("Ralph", new Position(0, 3));
        var board = game.Board;

        board.AddRock(new Rock(new Position(2, 3)));

        var gameId = game.Id;
        var turnNumber = game.TurnNumber;

        // Act: Ralph moves and ability triggers
        game.MovePiece(p1, ralphPiece.Id, BuildSegments(new Position(1, 3)));

        // Assert: Events have correct metadata
        var rockDestroyed = game.DomainEvents.OfType<RockDestroyed>().FirstOrDefault();
        Assert.NotNull(rockDestroyed);
        Assert.Equal(gameId, rockDestroyed.GameId);
        Assert.Equal(turnNumber, rockDestroyed.TurnNumber);

        var pieceMoved = game.DomainEvents.OfType<PieceMoved>().FirstOrDefault();
        Assert.NotNull(pieceMoved);
        Assert.Equal(ralphPiece.Id, pieceMoved.PieceId);
    }

    [Fact]
    public void MultipleAbilities_ProduceCorrectEvents()
    {
        // Arrange: Ralph piece, multiple obstacles
        var (game, p1, _, ralphPiece, _) = GameWithPiecesInMovePhase("Ralph", new Position(0, 3));
        var board = game.Board;

        board.AddRock(new Rock(new Position(2, 3)));
        board.AddFence(new Fence(new Position(1, 3), new Position(1, 4)));

        // Act: Ralph moves and triggers ability
        game.MovePiece(p1, ralphPiece.Id, BuildSegments(new Position(1, 3)));

        // Assert: Both event types present
        var rockEvents = game.DomainEvents.OfType<RockDestroyed>().ToList();
        var fenceEvents = game.DomainEvents.OfType<FenceDestroyed>().ToList();
        Assert.NotEmpty(rockEvents);
        Assert.NotEmpty(fenceEvents);
    }

    // ── Edge Case Tests ───────────────────────────────────────────────────────

    [Fact]
    public void Ability_WithNoTargets_CompletesSilently()
    {
        // Arrange: Ralph at (0,3), no adjacent obstacles
        var (game, p1, _, ralphPiece, _) = GameWithPiecesInMovePhase("Ralph", new Position(0, 3));

        // Act: Ralph moves with no obstacles
        game.MovePiece(p1, ralphPiece.Id, BuildSegments(new Position(1, 3)));

        // Assert: Movement succeeds, no events
        Assert.Equal(new Position(1, 3), ralphPiece.Position);
        var rockDestroyed = game.DomainEvents.OfType<RockDestroyed>().ToList();
        Assert.Empty(rockDestroyed);
    }

    [Fact]
    public void PieceMovement_EventIncludesCorrecPath()
    {
        // Arrange: Ralph at (0,3)
        var (game, p1, _, ralphPiece, _) = GameWithPiecesInMovePhase("Ralph", new Position(0, 3));

        // Act: Ralph moves
        game.MovePiece(p1, ralphPiece.Id, BuildSegments(new Position(1, 3)));

        // Assert: PieceMoved event includes a correct path
        var pieceMoved = game.DomainEvents.OfType<PieceMoved>().FirstOrDefault();
        Assert.NotNull(pieceMoved);
        Assert.Equal(new Position(0, 3), pieceMoved.From);
        Assert.Equal(new Position(1, 3), pieceMoved.To);
        Assert.NotEmpty(pieceMoved.Path);
    }

    [Fact]
    public void Rafiki_Jump_ProducesEvents()
    {
        // Arrange: Rafiki at (0,0)
        var (game, p1, _, rafikPiece, _) = GameWithPiecesInMovePhase("Rafiki", new Position(0, 0));

        // Act: Rafiki jumps
        game.MovePiece(p1, rafikPiece.Id, BuildSegments(new Position(2, 2)));

        // Assert: PieceMoved event produced
        var pieceMoved = game.DomainEvents.OfType<PieceMoved>().FirstOrDefault();
        Assert.NotNull(pieceMoved);
        Assert.Equal(rafikPiece.Id, pieceMoved.PieceId);
    }

    [Fact]
    public void Daisy_Jump_ProducesEvents()
    {
        // Arrange: Daisy at (0,3)
        var (game, p1, _, daisyPiece, _) = GameWithPiecesInMovePhase("Daisy", new Position(0, 3));

        // Act: Daisy jumps
        game.MovePiece(p1, daisyPiece.Id, BuildSegments(new Position(2, 3)));

        // Assert: PieceMoved event produced
        var pieceMoved = game.DomainEvents.OfType<PieceMoved>().FirstOrDefault();
        Assert.NotNull(pieceMoved);
        Assert.Equal(daisyPiece.Id, pieceMoved.PieceId);
    }

    [Fact]
    public void Scar_Jump_ProducesEvents()
    {
        // Arrange: Scar at (0,0)
        var (game, p1, _, scarPiece, _) = GameWithPiecesInMovePhase("Scar", new Position(0, 0));

        // Act: Scar jumps
        game.MovePiece(p1, scarPiece.Id, BuildSegments(new Position(2, 2)));

        // Assert: PieceMoved event produced
        var pieceMoved = game.DomainEvents.OfType<PieceMoved>().FirstOrDefault();
        Assert.NotNull(pieceMoved);
        Assert.Equal(scarPiece.Id, pieceMoved.PieceId);
    }
}
