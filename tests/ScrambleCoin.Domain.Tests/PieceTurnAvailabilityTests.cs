using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.Factories;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Domain.Tests;

/// <summary>
/// Tests for Piece turn-availability conditions (Issue #59).
/// A piece with <see cref="Piece.AvailableFromTurn"/> set may only be placed on or after
/// that turn; placing earlier must throw a <see cref="DomainException"/>.
///
/// Authoritative turn values are taken from <c>SCRAMBLECOIN_OVERVIEW.md</c>.
/// </summary>
public class PieceTurnAvailabilityTests
{
    // ── Constructor / factory wiring ──────────────────────────────────────────

    [Fact]
    public void Piece_DefaultsAvailableFromTurn_ToNull()
    {
        var piece = new Piece("Mickey", Guid.NewGuid(), EntryPointType.Borders, MovementType.Orthogonal, 3, 1);
        Assert.Null(piece.AvailableFromTurn);
    }

    [Fact]
    public void Piece_ExplicitAvailableFromTurn_IsPreserved()
    {
        var piece = new Piece("Elsa", Guid.NewGuid(), EntryPointType.Borders, MovementType.Orthogonal, 4, 1, availableFromTurn: 2);
        Assert.Equal(2, piece.AvailableFromTurn);
    }

    [Fact]
    public void Piece_AvailableFromTurnBelowOne_Throws()
    {
        Assert.Throws<DomainException>(() =>
            new Piece("X", Guid.NewGuid(), EntryPointType.Borders, MovementType.Orthogonal, 1, 1, availableFromTurn: 0));
    }

    [Theory]
    [InlineData("Mickey", null)]
    [InlineData("Goofy", null)]
    [InlineData("Scrooge", null)]
    [InlineData("Elsa", 2)]
    [InlineData("Remy", 2)]
    [InlineData("Daisy", 2)]
    [InlineData("Scar", 3)]
    [InlineData("Rapunzel", 3)]
    [InlineData("Mike Wazowski", 3)]
    [InlineData("Merlin", 4)]
    public void PieceFactory_SetsAvailableFromTurn_PerOverview(string name, int? expected)
    {
        var piece = PieceFactory.Create(name, Guid.NewGuid());
        Assert.Equal(expected, piece.AvailableFromTurn);
    }

    // ── Placement guard ───────────────────────────────────────────────────────

    [Fact]
    public void PlacePiece_RestrictedPieceOnEarlierTurn_Rejected()
    {
        var (game, p1, _, _, _, restricted) = BuildGameWithRestrictedPieceAtTurn(restrictedFromTurn: 3, currentTurn: 2);

        var ex = Assert.Throws<DomainException>(() =>
            game.PlacePiece(p1, restricted.Id, new Position(0, 0)));

        Assert.Contains("cannot be placed before turn 3", ex.Message);
        Assert.False(restricted.IsOnBoard);
    }

    [Fact]
    public void PlacePiece_RestrictedPieceOnExactTurn_Accepted()
    {
        var (game, p1, _, _, _, restricted) = BuildGameWithRestrictedPieceAtTurn(restrictedFromTurn: 2, currentTurn: 2);

        game.PlacePiece(p1, restricted.Id, new Position(0, 0));

        Assert.True(restricted.IsOnBoard);
        Assert.Equal(new Position(0, 0), restricted.Position);
    }

    [Fact]
    public void PlacePiece_RestrictedPieceOnLaterTurn_Accepted()
    {
        var (game, p1, _, _, _, restricted) = BuildGameWithRestrictedPieceAtTurn(restrictedFromTurn: 2, currentTurn: 3);

        game.PlacePiece(p1, restricted.Id, new Position(0, 0));

        Assert.True(restricted.IsOnBoard);
    }

    // ── Replacement guard ─────────────────────────────────────────────────────

    [Fact]
    public void ReplacePiece_WithRestrictedPieceOnEarlierTurn_Rejected()
    {
        // Game seeded at turn 4, in PlacePhase, with an unrestricted piece already on the board.
        // The "restricted" replacement requires turn 5, so this must be rejected.
        var (game, p1, _, _, _, restricted, existing) = BuildGameWithExistingPlacedAndRestrictedPiece(
            restrictedFromTurn: 5, currentTurn: 4);

        var ex = Assert.Throws<DomainException>(() =>
            game.ReplacePiece(p1, existing.Id, restricted.Id));

        Assert.Contains("cannot be placed before turn 5", ex.Message);
        Assert.True(existing.IsOnBoard);
        Assert.False(restricted.IsOnBoard);
    }

    [Fact]
    public void ReplacePiece_WithRestrictedPieceOnExactTurn_Accepted()
    {
        var (game, p1, _, _, _, restricted, existing) =
            BuildGameWithExistingPlacedAndRestrictedPiece(restrictedFromTurn: 3, currentTurn: 3);

        game.ReplacePiece(p1, existing.Id, restricted.Id);

        Assert.True(restricted.IsOnBoard);
        Assert.False(existing.IsOnBoard);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a Game seeded at the given <paramref name="currentTurn"/>, in PlacePhase,
    /// with a "restricted" piece available only from <paramref name="restrictedFromTurn"/>.
    /// Both players have 5 lineup pieces. The restricted piece replaces p1Pieces[1] in p1's lineup.
    /// </summary>
    private static (Game game, Guid p1, Guid p2, List<Piece> p1Pieces, List<Piece> p2Pieces, Piece restricted)
        BuildGameWithRestrictedPieceAtTurn(int restrictedFromTurn, int currentTurn)
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var board = new Board();

        var restricted = new Piece(
            Guid.NewGuid(), "Restricted", p1,
            EntryPointType.Borders, MovementType.Orthogonal, 1, 1,
            availableFromTurn: restrictedFromTurn);

        var p1Pieces = Enumerable.Range(0, 4)
            .Select(i => new Piece(Guid.NewGuid(), $"P1Piece{i}", p1, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();
        p1Pieces.Add(restricted);

        var p2Pieces = Enumerable.Range(0, 5)
            .Select(i => new Piece(Guid.NewGuid(), $"P2Piece{i}", p2, EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();

        var game = new Game(p1, p2, board);
        game.SetLineup(p1, new Lineup(p1Pieces));
        game.SetLineup(p2, new Lineup(p2Pieces));
        game.Start();          // → turn 1, CoinSpawn
        game.AdvancePhase();   // → PlacePhase

        // Walk forward to the desired turn by skipping placement and move phases.
        while (game.TurnNumber < currentTurn)
        {
            game.SkipPlacement(p1);
            game.SkipPlacement(p2);
            // SkipPlacement auto-advances PlacePhase → MovePhase when both players act.
            // From MovePhase advance → next turn CoinSpawn → PlacePhase.
            game.AdvancePhase(); // CoinSpawn → PlacePhase (SkipPlacement-both already advanced through MovePhase to next-turn CoinSpawn)
        }

        game.ClearDomainEvents();
        return (game, p1, p2, p1Pieces, p2Pieces, restricted);
    }

    /// <summary>
    /// Like <see cref="BuildGameWithRestrictedPieceAtTurn"/> but also places one unrestricted
    /// piece on the board (so ReplacePiece can be tested) on the current turn.
    /// </summary>
    private static (Game game, Guid p1, Guid p2, List<Piece> p1Pieces, List<Piece> p2Pieces, Piece restricted, Piece existing)
        BuildGameWithExistingPlacedAndRestrictedPiece(int restrictedFromTurn, int currentTurn)
    {
        // Seed at currentTurn-1 PlacePhase so we can place the "existing" piece on turn currentTurn-1,
        // then advance once more so we're back in PlacePhase on currentTurn with that piece on the board.
        // Edge case: if currentTurn == 1 (no restriction test cares about this), just seed at turn 1 and place.

        var seedTurn = Math.Max(1, currentTurn - 1);
        var (game, p1, p2, p1Pieces, p2Pieces, restricted) =
            BuildGameWithRestrictedPieceAtTurn(restrictedFromTurn, seedTurn);

        var existing = p1Pieces[0]; // unrestricted starter
        game.PlacePiece(p1, existing.Id, new Position(0, 0));
        game.SkipPlacement(p2);
        // p1 placed (has a piece on board), p2 skipped (no pieces). PlacePhase → MovePhase auto-fired.
        // p1 still has an unmoved piece, so MovePhase does NOT auto-advance. Need 2 manual phase advances.
        if (currentTurn > seedTurn)
        {
            game.SkipMovement(p1);
            game.AdvancePhase(); // CoinSpawn → PlacePhase
        }

        game.ClearDomainEvents();
        return (game, p1, p2, p1Pieces, p2Pieces, restricted, existing);
    }
}
