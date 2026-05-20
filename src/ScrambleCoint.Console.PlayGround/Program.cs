using Microsoft.Extensions.Logging.Abstractions;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Services;
using ScrambleCoin.Console.PlayGround;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.ValueObjects;

// ── Setup ─────────────────────────────────────────────────────────────────────
var rng = new Random(42); // change to new Random(42) for a reproducible run
var board = new Board();
var p1 = Guid.NewGuid();
var p2 = Guid.NewGuid();
var game = new Game(p1, p2, board);

game.SetLineup(p1, CreateLineup(p1, "Walle", "Elsa", "Stitch", "Ralph", "Joy"));
game.SetLineup(p2, CreateLineup(p2, "Ursula", "Gaston", "Scar", "Maleficent", "Hades"));

// CoinSpawnService handles the full coin-spawn flow:
//   CoinSpawnSchedule.For() → tile selection → game.SpawnCoins() → game.AdvancePhase() → save
// The in-memory repository keeps the game in memory (no DB needed for the playground).
var repo = new InMemoryGameRepository(game);
var coinSpawnService = new CoinSpawnService(repo, rng, NullLogger<CoinSpawnService>.Instance);

Banner("SCRAMBLECOIN PLAYGROUND");
PrintStatus(game);
Step("Lineups set — starting game");

game.Start();
PrintStatus(game);

// ── Turn loop ─────────────────────────────────────────────────────────────────
var viewer = new InteractiveGameViewer(game, p1, p2);
for (var turn = 1; turn <= Game.TotalTurns; turn++)
{
    Banner($"TURN {turn}");

    // 1. Coin Spawn — service handles schedule, tile selection, SpawnCoins, AdvancePhase, save
    await coinSpawnService.ExecuteForGameAsync(game);
    Step($"Coins spawned — phase is now {game.CurrentPhase}");
    PrintBoard(game);

    // 2. Place Phase — both players act (phase auto-advances when both are done)
    PlaceForPlayer(game, p1, "P1");
    PlaceForPlayer(game, p2, "P2");
    Step("Both players placed/skipped → MovePhase");
    PrintBoard(game);

    // 3. Move Phase — interactive viewer + automated moves
    viewer.DisplayTurn();
    MoveAllPieces(game, p1, "P1");
    MoveAllPieces(game, p2, "P2");
    Step($"Turn {turn} complete → scores: P1={game.GetScore(p1)}  P2={game.GetScore(p2)}");
    PrintStatus(game);
}

// ── Results ───────────────────────────────────────────────────────────────────
Banner("GAME OVER");
var s1 = game.GetScore(p1);
var s2 = game.GetScore(p2);
Console.WriteLine($"  P1 score : {s1}");
Console.WriteLine($"  P2 score : {s2}");
Console.WriteLine($"  Result   : {(s1 > s2 ? "P1 wins 🏆" : s2 > s1 ? "P2 wins 🏆" : "Draw 🤝")}");
Console.ReadLine();

// ── Helpers ───────────────────────────────────────────────────────────────────

Lineup CreateLineup(Guid playerId, params string[] names)
{
    var pieces = names.Select(name => new Piece(
        id: Guid.NewGuid(),
        name: name,
        playerId: playerId,
        entryPointType: EntryPointType.Borders,
        movementType: MovementType.Orthogonal,
        maxDistance: 3,
        movesPerTurn: 1)).ToList();
    return new Lineup(pieces);
}

void PlaceForPlayer(Game g, Guid playerId, string label)
{
    var lineup = playerId == p1 ? g.LineupPlayerOne! : g.LineupPlayerTwo!;
    var onBoard = g.PiecesOnBoard.TryGetValue(playerId, out var cnt) ? cnt : 0;

    if (onBoard >= Game.MaxPiecesOnBoard)
    {
        Console.WriteLine($"  {label}: already at max pieces ({Game.MaxPiecesOnBoard}) — skipping");
        g.SkipPlacement(playerId);
        return;
    }

    var piece = lineup.Pieces.FirstOrDefault(p => !p.IsOnBoard);
    if (piece is null)
    {
        Console.WriteLine($"  {label}: no pieces left — skipping");
        g.SkipPlacement(playerId);
        return;
    }

    var pos = FindEntryPoint(g, piece.EntryPointType, preferLeft: playerId == p1);
    if (pos is null)
    {
        Console.WriteLine($"  {label}: no free entry point — skipping");
        g.SkipPlacement(playerId);
        return;
    }

    g.PlacePiece(playerId, piece.Id, pos);
    Console.WriteLine($"  {label}: placed {piece.Name} at {pos}");
}

Position? FindEntryPoint(Game g, EntryPointType type, bool preferLeft)
{
    var col = preferLeft ? 0 : Board.Size - 1;
    for (var row = 0; row < Board.Size; row++)
    {
        var pos = new Position(row, col);
        if (g.Board.GetTile(pos).IsEmpty
            && !g.Board.IsObstacleCovering(pos)
            && Board.IsValidEntryPoint(pos, type))
            return pos;
    }
    for (var r = 0; r < Board.Size; r++)
    for (var c = 0; c < Board.Size; c++)
    {
        var pos = new Position(r, c);
        if (g.Board.GetTile(pos).IsEmpty
            && !g.Board.IsObstacleCovering(pos)
            && Board.IsValidEntryPoint(pos, type))
            return pos;
    }
    return null;
}

void MoveAllPieces(Game g, Guid playerId, string label)
{
    if (g.CurrentPhase != TurnPhase.MovePhase) return;
    var lineup = playerId == p1 ? g.LineupPlayerOne! : g.LineupPlayerTwo!;

    foreach (var piece in lineup.Pieces.Where(p => p.IsOnBoard).ToList())
    {
        if (g.CurrentPhase != TurnPhase.MovePhase) break;

        var dest = FindAdjacentFree(g, piece.Position!, piece.MovementType);
        if (dest is null)
        {
            Console.WriteLine($"  {label}: {piece.Name} at {piece.Position} — no valid move (skip)");
            g.MovePiece(playerId, piece.Id, [[]]);
        }
        else
        {
            Console.WriteLine($"  {label}: {piece.Name} moves {piece.Position} → {dest}");
            g.MovePiece(playerId, piece.Id, [[dest]]);
        }
    }
}

Position? FindAdjacentFree(Game g, Position from, MovementType mt)
{
    (int dr, int dc)[] deltas = mt == MovementType.Diagonal
        ? [(-1, -1), (-1, 1), (1, -1), (1, 1)]
        : [(-1, 0), (1, 0), (0, -1), (0, 1)];

    return deltas
        .Select(d => (row: from.Row + d.dr, col: from.Col + d.dc))
        .Where(c => c.row is >= 0 and < Board.Size && c.col is >= 0 and < Board.Size)
        .Select(c => new Position(c.row, c.col))
        .Where(p => g.Board.IsPassable(from, p))
        .Where(p => g.Board.GetTile(p).AsPiece is null)
        #pragma warning disable CA5394
        .OrderBy(_ => rng.Next())
        #pragma warning restore CA5394
        .FirstOrDefault();
}

void PrintBoard(Game g)
{
    Console.WriteLine();
    Console.WriteLine("      0     1     2     3     4     5     6     7");
    Console.WriteLine("   ┌─────┬─────┬─────┬─────┬─────┬─────┬─────┬─────┐");
    for (var row = 0; row < Board.Size; row++)
    {
        Console.Write($" {row} │");
        for (var col = 0; col < Board.Size; col++)
        {
            var pos = new Position(row, col);
            var tile = g.Board.GetTile(pos);
            string cell;
            if (g.Board.IsObstacleCovering(pos))            cell = "█████";
            else if (tile.AsPiece is { } piece)             cell = piece.PlayerId == p1 ? $" 1:{piece.Name[..2]} " : $" 2:{piece.Name[..2]} ";
            else if (tile.AsCoin is { } coin)               cell = coin.CoinType == CoinType.Gold ? "  $G " : "  $  ";
            else                                            cell = "     ";
            Console.Write(cell + "│");
        }
        Console.WriteLine($" {row}");
        if (row < Board.Size - 1)
            Console.WriteLine("   ├─────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┤");
    }
    Console.WriteLine("   └─────┴─────┴─────┴─────┴─────┴─────┴─────┴─────┘");
    Console.WriteLine("      0     1     2     3     4     5     6     7");
    Console.WriteLine($"   P1 score: {g.GetScore(p1)}  │  P2 score: {g.GetScore(p2)}  │  Phase: {g.CurrentPhase?.ToString() ?? "—"}");
    Console.WriteLine();
}

void PrintStatus(Game g)
{
    Console.WriteLine($"  Status: {g.Status}  |  Turn: {g.TurnNumber}  |  Phase: {g.CurrentPhase?.ToString() ?? "—"}");
    Console.WriteLine($"  P1: {g.GetScore(p1)} pts   P2: {g.GetScore(p2)} pts");
    Console.WriteLine();
}

void Banner(string title)
{
    var line = new string('─', title.Length + 4);
    Console.WriteLine();
    Console.WriteLine($"┌{line}┐");
    Console.WriteLine($"│  {title}  │");
    Console.WriteLine($"└{line}┘");
}

void Step(string message)
{
    Console.WriteLine($"\n▶  {message}");
    Console.Write("   [Enter to continue] ");
    Console.ReadLine();
}

// ── In-memory repository (playground only) ────────────────────────────────────

/// <summary>
/// Minimal IGameRepository that keeps a single game in memory.
/// No EF Core or SQL — used only by the Playground console app.
/// </summary>
sealed class InMemoryGameRepository(Game initial) : IGameRepository
{
    private Game _game = initial;

    public Task<Game> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_game);

    public Task SaveAsync(Game game, CancellationToken cancellationToken = default)
    {
        _game = game;
        game.ClearDomainEvents();
        return Task.CompletedTask;
    }
}
