using ScrambleCoin.Application.Services;
using ScrambleCoin.Application.Services.Villains;
using ScrambleCoin.Application.Services.Villains.Implementations;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Factories;
using ScrambleCoin.Domain.Obstacles;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Application.Tests;

/// <summary>
/// Integration-style unit tests for <see cref="GreedyVillainStrategy"/> (Issue #41).
/// The strategy is exercised against a real <see cref="Game"/>/<see cref="Board"/> so that every
/// action it produces is verified to be legal for the domain to apply. Covers the four acceptance
/// criteria: placing a piece, skipping placement at the 3-piece cap, moving toward the nearest coin,
/// and skipping movement when no legal move exists.
/// </summary>
public class GreedyVillainStrategyTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a solo-style game (bot = PlayerOne, villain = PlayerTwo) using the Elsa lineup for the
    /// villain and the default lineup for the bot, then advances it to <see cref="TurnPhase.CoinSpawn"/>
    /// on turn 1. Any supplied coins are spawned before the caller advances further.
    /// </summary>
    private static (Game game, Guid bot, Guid villain, Lineup villainLineup) NewGameAtCoinSpawn(
        IReadOnlyList<Rock>? rocks = null,
        IEnumerable<(Position Position, CoinType CoinType)>? coins = null)
    {
        var bot = Guid.NewGuid();
        var villain = Guid.NewGuid();

        var board = new Board();
        if (rocks is not null)
            foreach (var rock in rocks)
                board.AddRock(rock);

        var game = new Game(Guid.NewGuid(), bot, villain, board)
        {
            GameMode = GameMode.Solo,
            VillainId = VillainRegistry.Elsa.Id
        };

        var villainLineup = VillainRegistry.Elsa.GetLineup(villain);
        game.SetLineup(bot, VillainRegistry.GetDefaultLineup(bot));
        game.SetLineup(villain, villainLineup);

        game.Start(); // → CoinSpawn, turn 1

        if (coins is not null)
            game.SpawnCoins(coins);

        return (game, bot, villain, villainLineup);
    }

    private static int ManhattanDistance(Position a, Position b) =>
        Math.Abs(a.Row - b.Row) + Math.Abs(a.Col - b.Col);

    /// <summary>
    /// Builds a solo game (bot = PlayerOne, villain = PlayerTwo) where the villain owns the piece
    /// produced by <paramref name="villainPieceFactory"/> (placed first in a 5-piece lineup padded with
    /// orthogonal border fillers), spawns the given coins, places only that villain piece on
    /// <paramref name="villainStart"/>, and drives the game into the villain's
    /// <see cref="TurnPhase.MovePhase"/>. The bot places nothing, so the villain becomes the active mover.
    /// Returns the live <see cref="Game"/>, the player ids, and the placed villain piece.
    /// </summary>
    private static (Game game, Guid villain, Piece villainPiece) NewGameInVillainMovePhase(
        Func<Guid, Piece> villainPieceFactory,
        Position villainStart,
        IEnumerable<(Position Position, CoinType CoinType)> coins)
    {
        var bot = Guid.NewGuid();
        var villain = Guid.NewGuid();
        var board = new Board();

        var villainPiece = villainPieceFactory(villain);
        var fillers = Enumerable.Range(0, Lineup.RequiredPieceCount - 1)
            .Select(i => new Piece(
                Guid.NewGuid(), $"VillainFill{i}", villain,
                EntryPointType.Borders, MovementType.Orthogonal, 1, 1))
            .ToList();
        var villainLineup = new Lineup(new[] { villainPiece }.Concat(fillers));

        var game = new Game(Guid.NewGuid(), bot, villain, board)
        {
            GameMode = GameMode.Solo,
            VillainId = VillainRegistry.Elsa.Id
        };
        game.SetLineup(bot, VillainRegistry.GetDefaultLineup(bot));
        game.SetLineup(villain, villainLineup);

        game.Start(); // → CoinSpawn, turn 1
        game.SpawnCoins(coins);

        game.AdvancePhase(); // CoinSpawn → PlacePhase
        game.PlacePiece(villain, villainPiece.Id, villainStart);
        game.SkipPlacement(bot); // → MovePhase (bot has 0 pieces; villain becomes the active mover)

        return (game, villain, villainPiece);
    }

    // ── 1. Placement ──────────────────────────────────────────────────────────

    [Fact]
    public void DecideAction_InPlacePhase_ReturnsPlacementOnLegalEntryTileForLineupPiece()
    {
        // Arrange: game in PlacePhase, villain has acted nothing yet.
        var (game, _, villain, villainLineup) = NewGameAtCoinSpawn(
            coins: [(new Position(3, 3), CoinType.Silver)]);
        game.AdvancePhase(); // CoinSpawn → PlacePhase
        var strategy = new ElsaStrategy();

        // Act
        var action = strategy.DecideAction(game, villain);

        // Assert: a real lineup piece is placed on a legal, empty entry tile.
        var placement = Assert.IsType<PlacementAction>(action);
        var piece = Assert.Single(villainLineup.Pieces, p => p.Id == placement.PieceId);
        Assert.True(
            Board.IsValidEntryPoint(placement.Position, piece.EntryPointType),
            $"Position {placement.Position} must be a legal entry point for {piece.EntryPointType}.");
        Assert.True(game.Board.IsEmpty(placement.Position));
    }

    // ── 2. Skip placement at the 3-piece cap ────────────────────────────────────

    [Fact]
    public void DecideAction_InPlacePhaseAtThreePieceCap_ReturnsSkipPlacement()
    {
        // Arrange: drive three turns, letting the villain place one piece per PlacePhase using its
        // own (domain-legal) decisions, until it holds the maximum of three pieces on the board.
        var (game, bot, villain, _) = NewGameAtCoinSpawn();
        var strategy = new ElsaStrategy();

        for (var turn = 0; turn < Game.MaxPiecesOnBoard; turn++)
        {
            game.AdvancePhase(); // CoinSpawn → PlacePhase
            var placement = Assert.IsType<PlacementAction>(strategy.DecideAction(game, villain));
            game.PlacePiece(villain, placement.PieceId, placement.Position);
            game.SkipPlacement(bot); // both acted → MovePhase (bot has 0 pieces, villain becomes active)
            game.SkipMovement(villain); // advance to the next turn's CoinSpawn
        }

        // Now in turn 4's CoinSpawn with three villain pieces on the board.
        Assert.Equal(Game.MaxPiecesOnBoard, game.GetPiecesOnBoardCount(villain));
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        // Act
        var action = strategy.DecideAction(game, villain);

        // Assert
        Assert.IsType<SkipPlacementAction>(action);
    }

    // ── 3. Move toward the nearest coin ─────────────────────────────────────────

    [Fact]
    public void DecideAction_InMovePhase_MovesPieceTowardNearestCoin()
    {
        // Arrange: Mickey (orthogonal) at (0,0); the only coin is at (0,5).
        var coin = new Position(0, 5);
        var (game, bot, villain, villainLineup) = NewGameAtCoinSpawn(
            coins: [(coin, CoinType.Silver)]);
        var mickey = villainLineup.Pieces[0]; // Mickey is first in Elsa's lineup
        var start = new Position(0, 0);

        game.AdvancePhase(); // CoinSpawn → PlacePhase
        game.PlacePiece(villain, mickey.Id, start);
        game.SkipPlacement(bot); // → MovePhase (bot has 0 pieces; villain becomes the active mover)

        var strategy = new ElsaStrategy();

        // Act
        var action = strategy.DecideAction(game, villain);

        // Assert: the chosen step strictly reduces the distance to the nearest coin.
        var movement = Assert.IsType<MovementAction>(action);
        Assert.Equal(mickey.Id, movement.PieceId);
        var destination = movement.Segments.SelectMany(s => s).Last();
        Assert.True(
            ManhattanDistance(destination, coin) < ManhattanDistance(start, coin),
            $"Destination {destination} (dist {ManhattanDistance(destination, coin)}) must be closer " +
            $"to coin {coin} than start {start} (dist {ManhattanDistance(start, coin)}).");

        var exception = Record.Exception((Action?)ApplyMove);
        Assert.Null(exception);
        Assert.Equal(destination, mickey.Position);
        return;

        // Round-trip through the real domain engine: the produced single-segment move must be legal to
        // apply, and the piece must actually land on the destination tile, the strategy reported.
        void ApplyMove() => game.MovePiece(villain, movement.PieceId, movement.Segments);
    }

    // ── 3b. Multi-segment move stays domain-legal across an ice-patch slide ──────

    [Fact]
    public void DecideAction_InMovePhaseWithMultiSegmentPieceOverIcePatch_ProducesDomainLegalSlideMove()
    {
        // Arrange: Anna (3× Orthogonal, MovesPerTurn 3) starts in the corner at (0,0); the only coin is
        // far to the east at (0,7). An ice patch sits at (0,1) — the very tile Anna's first step toward
        // the coin lands on. The domain slides the piece one extra tile (→ (0,2)) after that step, so the
        // strategy MUST start its second segment from the post-slide tile. If it instead advanced the
        // cursor naively to (0,1), the second segment would target (0,2) — the tile the piece already
        // occupies after the slide — and the domain would reject the multi-segment move.
        var coin = new Position(0, 7);
        var icePatch = new Position(0, 1);
        var start = new Position(0, 0);

        var (game, villain, anna) = NewGameInVillainMovePhase(
            owner => PieceFactory.Create("Anna", owner),
            start,
            [(coin, CoinType.Silver)]);

        game.Board.PlaceIcePatch(icePatch);

        var strategy = new ElsaStrategy();

        // Act
        var action = strategy.DecideAction(game, villain);

        // Assert: a multi-segment movement is produced whose first step lands on the ice patch.
        var movement = Assert.IsType<MovementAction>(action);
        Assert.Equal(anna.Id, movement.PieceId);
        Assert.Equal(icePatch, movement.Segments[0][^1]);

        var exception = Record.Exception((Action?)ApplyMove);
        Assert.Null(exception);

        // After: step 1 (0,0)→(0,1) slides to (0,2); step 2 → (0,3); step 3 → (0,4).
        Assert.Equal(new Position(0, 4), anna.Position);
        return;

        // The whole point: applying the strategy's move to the real domain must NOT throw, proving the
        // ice-slide simulation kept every inter-segment cursor aligned with the domain's resolution.
        void ApplyMove() => game.MovePiece(villain, movement.PieceId, movement.Segments);
    }



    [Fact]
    public void DecideAction_InMovePhaseWithFullyBlockedPiece_ReturnsSkipMovement()
    {
        // Arrange: Mickey (orthogonal) cornered at (0,0); both in-bounds neighbours are rocked,
        // so no legal step exists even though a coin is present at (7,7).
        var rocks = new[] { new Rock(new Position(0, 1)), new Rock(new Position(1, 0)) };
        var (game, bot, villain, villainLineup) = NewGameAtCoinSpawn(
            rocks: rocks,
            coins: [(new Position(7, 7), CoinType.Silver)]);
        var mickey = villainLineup.Pieces[0];

        game.AdvancePhase(); // CoinSpawn → PlacePhase
        game.PlacePiece(villain, mickey.Id, new Position(0, 0));
        game.SkipPlacement(bot); // → MovePhase, villain active

        var strategy = new ElsaStrategy();

        // Act
        var action = strategy.DecideAction(game, villain);

        // Assert
        Assert.IsType<SkipMovementAction>(action);
    }

    // ── Extra: no coins on board → skip movement ────────────────────────────────

    [Fact]
    public void DecideAction_InMovePhaseWithNoCoins_ReturnsSkipMovement()
    {
        // Arrange: Mickey on board but no coins exist anywhere.
        var (game, bot, villain, villainLineup) = NewGameAtCoinSpawn();
        var mickey = villainLineup.Pieces[0];

        game.AdvancePhase(); // CoinSpawn → PlacePhase
        game.PlacePiece(villain, mickey.Id, new Position(0, 0));
        game.SkipPlacement(bot); // → MovePhase, villain active

        var strategy = new ElsaStrategy();

        // Act
        var action = strategy.DecideAction(game, villain);

        // Assert
        Assert.IsType<SkipMovementAction>(action);
    }
}
