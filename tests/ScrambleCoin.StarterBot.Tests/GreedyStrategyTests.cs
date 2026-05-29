using ScrambleCoin.StarterBot.Models;

namespace ScrambleCoin.StarterBot.Tests;

/// <summary>
/// Unit tests for <see cref="GreedyStrategy"/>.
/// All tests use in-memory board states — no HTTP, no disk I/O.
/// </summary>
public sealed class GreedyStrategyTests
{
    private readonly GreedyStrategy _sut = new();

    // ═══════════════════════════════════════════════════════════════════════════
    // DecidePlacement
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DecidePlacement_BorderPiece_ReturnsPlaceDecision()
    {
        // Mickey is a "Borders" entry-point piece
        var piece = MakePiece("Mickey", isOnBoard: false);
        var state = MakeEmptyState();

        var decision = _sut.DecidePlacement(state, piece);

        Assert.IsType<PlacementDecision.Place>(decision);
    }

    [Fact]
    public void DecidePlacement_BorderPiece_PlacesOnNonCornerBorderTile()
    {
        var piece = MakePiece("Mickey", isOnBoard: false);
        var state = MakeEmptyState();

        var place = (PlacementDecision.Place)_sut.DecidePlacement(state, piece);

        // The position must be on the border but NOT a corner
        var pos = place.Position;
        var isBorder = pos.Row == 0 || pos.Row == 7 || pos.Col == 0 || pos.Col == 7;
        var isCorner = (pos.Row is 0 or 7) && (pos.Col is 0 or 7);

        Assert.True(isBorder,  $"Expected a border tile but got ({pos.Row},{pos.Col})");
        Assert.False(isCorner, $"Border piece must NOT be placed on a corner but got ({pos.Row},{pos.Col})");
    }

    [Fact]
    public void DecidePlacement_CornerPiece_PlacesOnCornerFirst()
    {
        // Donald is a "Corners" entry-point piece
        var piece = MakePiece("Donald", isOnBoard: false);
        var state = MakeEmptyState();

        var place = (PlacementDecision.Place)_sut.DecidePlacement(state, piece);
        var pos = place.Position;

        var isCorner = (pos.Row is 0 or 7) && (pos.Col is 0 or 7);
        Assert.True(isCorner, $"Expected a corner tile but got ({pos.Row},{pos.Col})");
    }

    [Fact]
    public void DecidePlacement_AnywherePiece_ReturnsPlaceDecision()
    {
        // Flynn can go "Anywhere" — at minimum it should return a Place
        var piece = MakePiece("Flynn", isOnBoard: false);
        var state = MakeEmptyState();

        var decision = _sut.DecidePlacement(state, piece);

        Assert.IsType<PlacementDecision.Place>(decision);
    }

    [Fact]
    public void DecidePlacement_PlaceDecision_ContainsPieceId()
    {
        var piece = MakePiece("Elsa", isOnBoard: false);
        var state = MakeEmptyState();

        var place = (PlacementDecision.Place)_sut.DecidePlacement(state, piece);

        Assert.Equal(piece.PieceId, place.PieceId);
    }

    [Fact]
    public void DecidePlacement_ObstacleBlockingFirstBorderTile_PicksNextFreeTile()
    {
        // Place an obstacle at (0,1) — the first non-corner border tile
        var piece = MakePiece("Mickey", isOnBoard: false);
        var state = MakeEmptyState();
        state.Board.Tiles.Add(new TileState { Position = new Position(0, 1), IsObstacle = true });

        var place = (PlacementDecision.Place)_sut.DecidePlacement(state, piece);

        Assert.NotEqual(new Position(0, 1), place.Position, new PositionComparer());
    }

    [Fact]
    public void DecidePlacement_CoinOnFirstBorderTile_PicksNextFreeTile()
    {
        // A coin occupies (0,1) — strategy treats coins as occupied
        var piece = MakePiece("Mickey", isOnBoard: false);
        var state = MakeEmptyState();
        state.AvailableCoins.Add(new CoinState { Position = new Position(0, 1) });

        var place = (PlacementDecision.Place)_sut.DecidePlacement(state, piece);

        Assert.NotEqual(new Position(0, 1), place.Position, new PositionComparer());
    }

    [Fact]
    public void DecidePlacement_PieceOccupyingFirstBorderTile_PicksNextFreeTile()
    {
        var piece = MakePiece("Mickey", isOnBoard: false);
        var state = MakeEmptyState();
        state.Board.Tiles.Add(new TileState
        {
            Position = new Position(0, 1),
            Occupant = new TileOccupant { Type = "piece" }
        });

        var place = (PlacementDecision.Place)_sut.DecidePlacement(state, piece);

        Assert.NotEqual(new Position(0, 1), place.Position,
            new PositionComparer());
    }

    [Fact]
    public void DecidePlacement_AllBorderTilesOccupied_ReturnsSkip()
    {
        var piece = MakePiece("Mickey", isOnBoard: false);
        var state = MakeEmptyState();

        // Block every non-corner border tile with obstacles
        for (var col = 1; col <= 6; col++)
        {
            state.Board.Tiles.Add(new TileState { Position = new Position(0, col),   IsObstacle = true });
            state.Board.Tiles.Add(new TileState { Position = new Position(7, col),   IsObstacle = true });
        }
        for (var row = 1; row <= 6; row++)
        {
            state.Board.Tiles.Add(new TileState { Position = new Position(row, 0),   IsObstacle = true });
            state.Board.Tiles.Add(new TileState { Position = new Position(row, 7),   IsObstacle = true });
        }

        var decision = _sut.DecidePlacement(state, piece);

        Assert.IsType<PlacementDecision.Skip>(decision);
    }

    [Fact]
    public void DecidePlacement_UnknownPieceName_TreatedAsBorderEntryType()
    {
        // An unknown piece name should fall back to "Borders" entry type
        var piece = MakePiece("UnknownHero", isOnBoard: false);
        var state = MakeEmptyState();

        var place = (PlacementDecision.Place)_sut.DecidePlacement(state, piece);
        var pos = place.Position;

        var isBorder = pos.Row == 0 || pos.Row == 7 || pos.Col == 0 || pos.Col == 7;
        var isCorner = (pos.Row is 0 or 7) && (pos.Col is 0 or 7);

        Assert.True(isBorder,  $"Fallback should land on a border tile, got ({pos.Row},{pos.Col})");
        Assert.False(isCorner, $"Fallback should not land on a corner, got ({pos.Row},{pos.Col})");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DecideMove
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DecideMove_NoCoins_ReturnsEmptySegmentsForEachMoveSlot()
    {
        var piece = MakePlacedPiece("Mickey", row: 3, col: 3, movesPerTurn: 1, movementType: "Orthogonal");
        var state = MakeEmptyState(); // no coins

        var decision = _sut.DecideMove(state, piece);

        Assert.Single(decision.Segments);
        Assert.Empty(decision.Segments[0]);
    }

    [Fact]
    public void DecideMove_NoCoins_SegmentCountMatchesMovesPerTurn()
    {
        var piece = MakePlacedPiece("Mickey", row: 3, col: 3, movesPerTurn: 2, movementType: "Orthogonal");
        var state = MakeEmptyState();

        var decision = _sut.DecideMove(state, piece);

        Assert.Equal(2, decision.Segments.Count);
    }

    [Fact]
    public void DecideMove_WithCoin_SegmentCountMatchesMovesPerTurn()
    {
        var piece = MakePlacedPiece("Mickey", row: 3, col: 3, movesPerTurn: 1, movementType: "Orthogonal");
        var state = MakeEmptyState();
        state.AvailableCoins.Add(new CoinState { Position = new Position(3, 5) });

        var decision = _sut.DecideMove(state, piece);

        Assert.Single(decision.Segments);
    }

    [Fact]
    public void DecideMove_WithCoin_ReturnsDecisionWithMatchingPieceId()
    {
        var piece = MakePlacedPiece("Mickey", row: 3, col: 3, movesPerTurn: 1, movementType: "Orthogonal");
        var state = MakeEmptyState();
        state.AvailableCoins.Add(new CoinState { Position = new Position(3, 5) });

        var decision = _sut.DecideMove(state, piece);

        Assert.Equal(piece.PieceId, decision.PieceId);
    }

    [Fact]
    public void DecideMove_OrthogonalPiece_StepsAreOrthogonal()
    {
        // Piece at (3,3); coin directly to the right at (3,6)
        var piece = MakePlacedPiece("Mickey", row: 3, col: 3, movesPerTurn: 1, movementType: "Orthogonal");
        var state = MakeEmptyState();
        state.AvailableCoins.Add(new CoinState { Position = new Position(3, 6) });

        var decision = _sut.DecideMove(state, piece);
        var step = decision.Segments[0].Single();

        // The step must be exactly one row or column away — not diagonal
        var dr = Math.Abs(step.Row - 3);
        var dc = Math.Abs(step.Col - 3);
        var isOrthogonal = (dr == 1 && dc == 0) || (dr == 0 && dc == 1);

        Assert.True(isOrthogonal, $"Expected orthogonal step, got ({step.Row},{step.Col}) from (3,3)");
    }

    [Fact]
    public void DecideMove_DiagonalPiece_StepsAreDiagonal()
    {
        // Piece at (3,3); coin at (5,5)
        var piece = MakePlacedPiece("Scar", row: 3, col: 3, movesPerTurn: 1, movementType: "Diagonal");
        var state = MakeEmptyState();
        state.AvailableCoins.Add(new CoinState { Position = new Position(5, 5) });

        var decision = _sut.DecideMove(state, piece);
        var step = decision.Segments[0].Single();

        var dr = Math.Abs(step.Row - 3);
        var dc = Math.Abs(step.Col - 3);
        var isDiagonal = dr == 1 && dc == 1;

        Assert.True(isDiagonal, $"Expected diagonal step, got ({step.Row},{step.Col}) from (3,3)");
    }

    [Fact]
    public void DecideMove_AllDirectionsBlocked_ReturnsEmptySegmentForBlockedSlot()
    {
        // Surround the piece with obstacles so it cannot move
        var piece = MakePlacedPiece("Mickey", row: 3, col: 3, movesPerTurn: 1, movementType: "Orthogonal");
        var state = MakeEmptyState();
        state.AvailableCoins.Add(new CoinState { Position = new Position(0, 0) });

        // Block all four orthogonal neighbours
        foreach (var (r, c) in new[] { (2, 3), (4, 3), (3, 2), (3, 4) })
            state.Board.Tiles.Add(new TileState { Position = new Position(r, c), IsObstacle = true });

        var decision = _sut.DecideMove(state, piece);

        Assert.Empty(decision.Segments[0]);
    }

    [Fact]
    public void DecideMove_MultiSegmentPiece_MovesInSubsequentSegmentsFromUpdatedPosition()
    {
        // Piece has 2 moves per turn; each should build from the prior segment's destination
        var piece = MakePlacedPiece("Mickey", row: 3, col: 0, movesPerTurn: 2, movementType: "Orthogonal");
        var state = MakeEmptyState();
        state.AvailableCoins.Add(new CoinState { Position = new Position(3, 7) }); // far right

        var decision = _sut.DecideMove(state, piece);

        // Both segments should be non-empty (piece should move twice toward the coin)
        Assert.Equal(2, decision.Segments.Count);
        // At least the first segment should not be empty since there's a clear path
        Assert.NotEmpty(decision.Segments[0]);
        Assert.NotEmpty(decision.Segments[1]);
    }

    [Fact]
    public void DecideMove_AnyDirectionPiece_CanStepDiagonally()
    {
        // "AnyDirection" pieces can step in any of the 8 directions
        var piece = MakePlacedPiece("Flynn", row: 4, col: 4, movesPerTurn: 1, movementType: "AnyDirection");
        var state = MakeEmptyState();
        // Coin diagonally above-left — the closest orthogonal and diagonal step should both be considered
        state.AvailableCoins.Add(new CoinState { Position = new Position(2, 2) });

        var decision = _sut.DecideMove(state, piece);

        // Should produce one non-empty segment
        Assert.Single(decision.Segments);
        Assert.NotEmpty(decision.Segments[0]);
    }

    [Fact]
    public void DecideMove_PieceAtBoardEdge_DoesNotStepOutOfBounds()
    {
        // Piece in the top-left area; coin further up would require stepping off the board
        var piece = MakePlacedPiece("Mickey", row: 0, col: 1, movesPerTurn: 1, movementType: "Orthogonal");
        var state = MakeEmptyState();
        state.AvailableCoins.Add(new CoinState { Position = new Position(0, 7) });

        var decision = _sut.DecideMove(state, piece);
        var step = decision.Segments[0].Single();

        Assert.InRange(step.Row, 0, 7);
        Assert.InRange(step.Col, 0, 7);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════════════

    private static PieceState MakePiece(string name, bool isOnBoard) => new()
    {
        PieceId      = Guid.NewGuid(),
        Name         = name,
        IsOnBoard    = isOnBoard,
        MovesPerTurn = 1,
        MovementType = "Orthogonal"
    };

    private static PieceState MakePlacedPiece(
        string name, int row, int col,
        int movesPerTurn = 1,
        string movementType = "Orthogonal") => new()
    {
        PieceId      = Guid.NewGuid(),
        Name         = name,
        IsOnBoard    = true,
        Position     = new Position(row, col),
        MovesPerTurn = movesPerTurn,
        MovementType = movementType
    };

    private static BoardState MakeEmptyState() => new()
    {
        Turn  = 1,
        Phase = "PlacePhase",
        Board = new BoardData()
    };

    private sealed class PositionComparer : IEqualityComparer<Position>
    {
        public bool Equals(Position? x, Position? y) => x?.Row == y?.Row && x?.Col == y?.Col;
        public int GetHashCode(Position p) => HashCode.Combine(p.Row, p.Col);
    }
}
