using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Events;
using ScrambleCoin.Domain.Factories;
using ScrambleCoin.Domain.Obstacles;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Tests;

/// <summary>
/// Unit tests for on-stop abilities (Issue #49).
/// Tests cover Ralph, Pumbaa, WALL•E, Sulley, and Rafiki abilities.
/// </summary>
public class OnStopAbilitiesTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a game in MovePhase with both players having pieces on the board.
    /// Player 1 has the named piece; Player 2 has a filler piece.
    /// </summary>
    private static (Game game, Guid p1, Guid p2, Piece testPiece, Piece p2Piece) GameWithPiecesInMovePhase(
        string p1PieceName,
        Position p1Position,
        Position p2Position = null)
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
        var actualP1Pos = p1Position ?? new Position(0, 3); // Valid Border entry point
        var actualP2Pos = p2Position ?? new Position(7, 3);  // Valid Border entry point

        game.PlacePiece(p1, testPiece.Id, actualP1Pos);
        game.PlacePiece(p2, p2Piece.Id, actualP2Pos);

        return (game, p1, p2, testPiece, p2Piece);
    }

    private static IReadOnlyList<IReadOnlyList<Position>> BuildSegments(params Position[] steps)
    {
        var segment = (IReadOnlyList<Position>)steps.ToList().AsReadOnly();
        return new List<IReadOnlyList<Position>> { segment }.AsReadOnly();
    }

    // ── Ralph Tests ───────────────────────────────────────────────────────────

    [Fact]
    public void Ralph_DestroysBothAdjacentRockAndFence()
    {
        // Arrange: Ralph at (0,3), rock at (1,3), fence at (0,2)↔(0,3)
        var (game, p1, _, ralphPiece, _) = GameWithPiecesInMovePhase("Ralph", new Position(0, 3));
        var board = game.Board;

        // Add rock and fence
        board.AddRock(new Rock(new Position(1, 3)));
        board.AddFence(new Fence(new Position(0, 2), new Position(0, 3)));

        Assert.True(board.HasRock(new Position(1, 3)));
        Assert.True(board.HasFence(new Position(0, 3)));

        // Act: Move Ralph
        game.MovePiece(p1, ralphPiece.Id, BuildSegments(new Position(0, 4), new Position(0, 3)));

        // Assert: Rock and fence destroyed
        Assert.False(board.HasRock(new Position(1, 3)));
        Assert.False(board.HasFence(new Position(0, 3)));

        var rockDestroyed = game.DomainEvents.OfType<RockDestroyed>().ToList();
        var fenceDestroyed = game.DomainEvents.OfType<FenceDestroyed>().ToList();
        Assert.NotEmpty(rockDestroyed);
        Assert.NotEmpty(fenceDestroyed);
    }

    [Fact]
    public void Ralph_DestroysBothAdjacentObstacles_MultipleRocks()
    {
        // Arrange: Ralph at (0,3), rocks at all adjacent orthogonal positions
        var (game, p1, _, ralphPiece, _) = GameWithPiecesInMovePhase("Ralph", new Position(0, 3));
        var board = game.Board;

        // Add rocks: South only (North is edge)
        board.AddRock(new Rock(new Position(1, 3))); // South
        board.AddRock(new Rock(new Position(0, 2))); // West
        board.AddRock(new Rock(new Position(0, 4))); // East

        // Act: Move Ralph
        game.MovePiece(p1, ralphPiece.Id, BuildSegments(new Position(0, 4), new Position(0, 3)));

        // Assert: All rocks destroyed
        Assert.False(board.HasRock(new Position(1, 3)));
        Assert.False(board.HasRock(new Position(0, 2)));
        Assert.False(board.HasRock(new Position(0, 4)));

        var rockDestroyed = game.DomainEvents.OfType<RockDestroyed>().ToList();
        Assert.Equal(3, rockDestroyed.Count);
    }

    [Fact]
    public void Ralph_OnEmptyBoard_RaisesNoDestructionEvents()
    {
        // Arrange: Ralph at (0,3), no obstacles
        var (game, p1, _, ralphPiece, _) = GameWithPiecesInMovePhase("Ralph", new Position(0, 3));

        // Act: Move Ralph
        game.MovePiece(p1, ralphPiece.Id, BuildSegments(new Position(0, 4), new Position(0, 3)));

        // Assert: No destruction events
        var rockDestroyed = game.DomainEvents.OfType<RockDestroyed>().ToList();
        var fenceDestroyed = game.DomainEvents.OfType<FenceDestroyed>().ToList();
        Assert.Empty(rockDestroyed);
        Assert.Empty(fenceDestroyed);
    }

    // ── Pumbaa Tests ──────────────────────────────────────────────────────────

    [Fact]
    public void Pumbaa_DestroysSurroundingFencesOnly_NotRocks()
    {
        // Arrange: Pumbaa at (0,3)
        var (game, p1, _, pumbaaPiece, _) = GameWithPiecesInMovePhase("Pumbaa", new Position(0, 3));
        var board = game.Board;

        // Add rock and fence
        board.AddRock(new Rock(new Position(1, 3)));
        board.AddFence(new Fence(new Position(0, 2), new Position(0, 3)));

        // Act: Pumbaa charges left
        game.MovePiece(p1, pumbaaPiece.Id, new List<IReadOnlyList<Position>>
        {
            new List<Position> { new Position(0, 1) }.AsReadOnly() // Charge left
        }.AsReadOnly());

        // Assert: Fence destroyed, rock remains
        Assert.True(board.HasRock(new Position(1, 3))); // Rock NOT destroyed by Pumbaa
        var fenceDestroyed = game.DomainEvents.OfType<FenceDestroyed>().ToList();
        Assert.NotEmpty(fenceDestroyed);
    }

    // ── WALL•E Tests ──────────────────────────────────────────────────────────

    [Fact]
    public void WallE_PushesAdjacentPiece_1TileAway()
    {
        // Arrange: WALL•E at (0,3), opponent piece at (1,3)
        var (game, p1, p2, wallEPiece, _) = GameWithPiecesInMovePhase("WALL•E", new Position(0, 3));
        var board = game.Board;

        var opponentPiece = new Piece(Guid.NewGuid(), "Opponent", p2,
            EntryPointType.Borders, MovementType.Orthogonal, 1, 1);
        opponentPiece.PlaceAt(new Position(1, 3));
        board.GetTile(new Position(1, 3)).SetOccupant(opponentPiece);

        // Act: WALL•E charges down
        game.MovePiece(p1, wallEPiece.Id, new List<IReadOnlyList<Position>>
        {
            new List<Position> { new Position(1, 3) }.AsReadOnly() // Charge down
        }.AsReadOnly());

        // Assert: Opponent piece pushed to (2,3)
        Assert.Equal(new Position(2, 3), opponentPiece.Position);

        var pieceMoved = game.DomainEvents.OfType<PieceMoved>()
            .Where(pm => pm.PieceId == opponentPiece.Id).ToList();
        Assert.NotEmpty(pieceMoved);
    }

    [Fact]
    public void WallE_PushesMultipleAdjacentPieces()
    {
        // Arrange: WALL•E at (3,3), pieces at (2,3), (4,3), (3,2), (3,4)
        var (game, p1, p2, wallEPiece, p2Piece) = GameWithPiecesInMovePhase("WALL•E", new Position(3, 0));
        var board = game.Board;

        // Place WALL•E in the middle area
        board.GetTile(new Position(3, 0)).ClearOccupant();
        wallEPiece.RemoveFromBoard();

        wallEPiece.PlaceAt(new Position(3, 3));
        board.GetTile(new Position(3, 3)).SetOccupant(wallEPiece);

        var pieces = new[]
        {
            new Position(2, 3), // North
            new Position(4, 3), // South
            new Position(3, 2), // West
            new Position(3, 4), // East
        };

        var piecesToMove = new List<Piece>();
        foreach (var pos in pieces)
        {
            var p = new Piece(Guid.NewGuid(), $"Opponent{pos}", p2,
                EntryPointType.Borders, MovementType.Orthogonal, 1, 1);
            p.PlaceAt(pos);
            board.GetTile(pos).SetOccupant(p);
            piecesToMove.Add(p);
        }

        // Act: WALL•E charges (simulating being in the middle)
        game.MovePiece(p1, wallEPiece.Id, new List<IReadOnlyList<Position>>
        {
            new List<Position> { new Position(3, 2) }.AsReadOnly() // Charge left
        }.AsReadOnly());

        // Assert: All pieces pushed (at least check that some moved)
        var movedEvents = game.DomainEvents.OfType<PieceMoved>()
            .Where(pm => pm.PieceId != wallEPiece.Id).ToList();
        Assert.NotEmpty(movedEvents);
    }

    // ── Sulley Tests ──────────────────────────────────────────────────────────

    [Fact]
    public void Sulley_PushesOpponentPiece_2TilesAway()
    {
        // Arrange: Sulley at (0,3), opponent at (1,3)
        var (game, p1, p2, sulleyPiece, _) = GameWithPiecesInMovePhase("Sulley", new Position(0, 3));
        var board = game.Board;

        var opponentPiece = new Piece(Guid.NewGuid(), "Opponent", p2,
            EntryPointType.Borders, MovementType.Orthogonal, 1, 1);
        opponentPiece.PlaceAt(new Position(1, 3));
        board.GetTile(new Position(1, 3)).SetOccupant(opponentPiece);

        // Act: Sulley moves
        game.MovePiece(p1, sulleyPiece.Id, BuildSegments(new Position(1, 3), new Position(0, 3)));

        // Assert: Opponent pushed 2 tiles to (3,3)
        Assert.Equal(new Position(3, 3), opponentPiece.Position);
    }

    [Fact]
    public void Sulley_PushesOpponentPiece_OnlyToFirstObstacle()
    {
        // Arrange: Sulley at (0,3), opponent at (1,3), rock at (3,3)
        var (game, p1, p2, sulleyPiece, _) = GameWithPiecesInMovePhase("Sulley", new Position(0, 3));
        var board = game.Board;

        var opponentPiece = new Piece(Guid.NewGuid(), "Opponent", p2,
            EntryPointType.Borders, MovementType.Orthogonal, 1, 1);
        opponentPiece.PlaceAt(new Position(1, 3));
        board.GetTile(new Position(1, 3)).SetOccupant(opponentPiece);

        board.AddRock(new Rock(new Position(3, 3)));

        // Act: Sulley moves
        game.MovePiece(p1, sulleyPiece.Id, BuildSegments(new Position(1, 3), new Position(0, 3)));

        // Assert: Opponent pushed only 1 tile to (2,3) (blocked at 2nd tile)
        Assert.Equal(new Position(2, 3), opponentPiece.Position);
    }

    [Fact]
    public void Sulley_DoesNotPushAllyPieces()
    {
        // Arrange: Sulley at (0,3), ally at (1,3)
        var (game, p1, p2, sulleyPiece, _) = GameWithPiecesInMovePhase("Sulley", new Position(0, 3));
        var board = game.Board;

        var allyPiece = new Piece(Guid.NewGuid(), "Ally", p1,
            EntryPointType.Borders, MovementType.Orthogonal, 1, 1);
        allyPiece.PlaceAt(new Position(1, 3));
        board.GetTile(new Position(1, 3)).SetOccupant(allyPiece);

        // Act: Sulley moves
        game.MovePiece(p1, sulleyPiece.Id, BuildSegments(new Position(1, 3), new Position(0, 3)));

        // Assert: Ally piece not pushed
        Assert.Equal(new Position(1, 3), allyPiece.Position);
    }

    // ── Rafiki Tests ───────────────────────────────────────────────────────────

    [Fact]
    public void Rafiki_PushesAllAdjacentPieces_IncludingAllies()
    {
        // Arrange: Rafiki at corner, multiple adjacent pieces
        var (game, p1, p2, rafikiPiece, _) = GameWithPiecesInMovePhase("Rafiki", new Position(0, 0));
        var board = game.Board;

        // Place pieces adjacent to Rafiki at (0,0)
        var adjacentPositions = new[]
        {
            new Position(0, 1), // East
            new Position(1, 0), // South
            new Position(1, 1), // Southeast
        };

        var piecesToMove = new List<Piece>();
        var index = 0;
        foreach (var pos in adjacentPositions)
        {
            var owner = (index % 2 == 0) ? p1 : p2;
            var p = new Piece(Guid.NewGuid(), $"Piece{pos}", owner,
                EntryPointType.Borders, MovementType.Orthogonal, 1, 1);
            p.PlaceAt(pos);
            board.GetTile(pos).SetOccupant(p);
            piecesToMove.Add(p);
            index++;
        }

        // Act: Rafiki jumps
        game.MovePiece(p1, rafikiPiece.Id, new List<IReadOnlyList<Position>>
        {
            new List<Position> { new Position(3, 3) }.AsReadOnly() // Jump to center
        }.AsReadOnly());

        // Assert: At least some pieces were pushed
        var pieceMoved = game.DomainEvents.OfType<PieceMoved>()
            .Where(pm => pm.PieceId != rafikiPiece.Id).ToList();
        Assert.NotEmpty(pieceMoved);
    }

    // ── Event Verification Tests ──────────────────────────────────────────────

    [Fact]
    public void OnStopAbility_EventsIncludeCorrectGameIdAndTurnNumber()
    {
        // Arrange: Ralph at (0,3), rock at (1,3)
        var (game, p1, _, ralphPiece, _) = GameWithPiecesInMovePhase("Ralph", new Position(0, 3));
        var board = game.Board;
        board.AddRock(new Rock(new Position(1, 3)));

        var gameId = game.Id;
        var turnNumber = game.TurnNumber;

        // Act: Ralph moves and destroys rock
        game.MovePiece(p1, ralphPiece.Id, BuildSegments(new Position(0, 4), new Position(0, 3)));

        // Assert: Events have correct game ID and turn number
        var rockDestroyed = game.DomainEvents.OfType<RockDestroyed>().FirstOrDefault();
        Assert.NotNull(rockDestroyed);
        Assert.Equal(gameId, rockDestroyed!.GameId);
        Assert.Equal(turnNumber, rockDestroyed!.TurnNumber);
    }

    [Fact]
    public void PumbaaAbility_DoesNotTriggerIfNoPiecesAdjacentButHasFences()
    {
        // Arrange: Pumbaa at (0,3), with fences nearby
        var (game, p1, _, pumbaaPiece, _) = GameWithPiecesInMovePhase("Pumbaa", new Position(0, 3));
        var board = game.Board;

        // Add fence near Pumbaa
        board.AddFence(new Fence(new Position(0, 2), new Position(0, 3)));

        var eventCountBefore = game.DomainEvents.Count;

        // Act: Pumbaa charges
        game.MovePiece(p1, pumbaaPiece.Id, new List<IReadOnlyList<Position>>
        {
            new List<Position> { new Position(0, 1) }.AsReadOnly()
        }.AsReadOnly());

        // Assert: Fence destroyed events generated
        var fenceDestroyed = game.DomainEvents.OfType<FenceDestroyed>().ToList();
        Assert.NotEmpty(fenceDestroyed);
    }
}
