using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Console.PlayGround;

/// <summary>
/// Interactive console game viewer that displays the board and allows
/// the user to see possible movements and select moves visually.
/// </summary>
public sealed class InteractiveGameViewer(Game game, Guid playerOneId, Guid playerTwoId)
{
    private const string P1 = "P1";
    private const string P2 = "P2";

    public void DisplayTurn()
    {
        if (game.CurrentPhase != TurnPhase.MovePhase)
            return;

        System.Console.Clear();
        System.Console.WriteLine($"\n╔═══════════════════════════════════╗");
        System.Console.WriteLine($"║  INTERACTIVE MOVE VIEWER - TURN {game.TurnNumber}  ║");
        System.Console.WriteLine($"╚═══════════════════════════════════╝\n");

        // Show whose turn it is (simplified: just show playable pieces)
        PrintBoardWithCoordinates();
        System.Console.WriteLine();

        var p1Pieces = game.LineupPlayerOne!.Pieces.Where(p => p.IsOnBoard).ToList();
        var p2Pieces = game.LineupPlayerTwo!.Pieces.Where(p => p.IsOnBoard).ToList();

        System.Console.WriteLine($"┌─ PLAYER 1 PIECES ─────────────────┐");
        foreach (var (idx, piece) in p1Pieces.Select((p, i) => (i + 1, p)))
        {
            var indicators = GetMovementIndicators(piece);
            System.Console.WriteLine($"│ [{idx}] {piece.Name,-20} @ {piece.Position}  {indicators}");
        }
        if (!p1Pieces.Any())
            System.Console.WriteLine($"│ (no pieces on board)");
        System.Console.WriteLine($"└───────────────────────────────────┘");

        System.Console.WriteLine();

        System.Console.WriteLine($"┌─ PLAYER 2 PIECES ─────────────────┐");
        foreach (var (idx, piece) in p2Pieces.Select((p, i) => (i + 1, p)))
        {
            var indicators = GetMovementIndicators(piece);
            System.Console.WriteLine($"│ [{idx}] {piece.Name,-20} @ {piece.Position}  {indicators}");
        }
        if (!p2Pieces.Any())
            System.Console.WriteLine($"│ (no pieces on board)");
        System.Console.WriteLine($"└───────────────────────────────────┘");

        System.Console.WriteLine();
        System.Console.WriteLine("Select a piece to see its possible moves:");
        System.Console.WriteLine("  [1-9]    = Select P1 piece by number");
        System.Console.WriteLine("  [Q-I]    = Select P2 piece by letter");
        System.Console.WriteLine("  [Space]  = Skip interactive mode");
        System.Console.Write("\n▶ Your choice: ");

        var input = System.Console.ReadLine()?.Trim().ToUpper() ?? "";

        if (input == " " || string.IsNullOrEmpty(input))
        {
            System.Console.WriteLine("\n[Skipping interactive mode]");
            return;
        }

        // Parse P1 selection (1-9)
        if (input.Length == 1 && char.IsDigit(input[0]))
        {
            var idx = int.Parse(input) - 1;
            if (idx >= 0 && idx < p1Pieces.Count)
            {
                ShowPieceMovements(p1Pieces[idx], P1);
            }
            else
            {
                System.Console.WriteLine($"\n✗ Invalid P1 piece number: {input}");
            }
            return;
        }

        // Parse P2 selection (A-I = indices 0-8)
        if (input.Length == 1 && char.IsLetter(input[0]))
        {
            var letterIdx = input[0] - 'A';
            if (letterIdx >= 0 && letterIdx < p2Pieces.Count)
            {
                ShowPieceMovements(p2Pieces[letterIdx], P2);
            }
            else
            {
                System.Console.WriteLine($"\n✗ Invalid P2 piece letter: {input}");
            }
            return;
        }

        System.Console.WriteLine($"\n✗ Invalid input: {input}");
    }

    private void ShowPieceMovements(Piece piece, string player)
    {
        System.Console.Clear();
        System.Console.WriteLine($"\n╔═══════════════════════════════════╗");
        System.Console.WriteLine($"║  {player}: {piece.Name,-25}║");
        System.Console.WriteLine($"╚═══════════════════════════════════╝\n");

        var validMoves = CalculatePossibleMoves(piece);

        System.Console.WriteLine($"Position: {piece.Position}");
        System.Console.WriteLine($"Movement Type: {piece.MovementType} | Max Distance: {piece.MaxDistance} | Moves/Turn: {piece.MovesPerTurn}");
        System.Console.WriteLine();

        PrintBoardWithHighlights(piece.Position!, validMoves);

        System.Console.WriteLine();
        System.Console.WriteLine("Legend:");
        System.Console.WriteLine("  🟦 = Piece position");
        System.Console.WriteLine("   🟩 = Possible move");
        System.Console.WriteLine("   🟥 = Blocked (obstacle, piece, or out of bounds)");
        System.Console.WriteLine();
        System.Console.WriteLine($"Total possible first moves: {validMoves.Count}");
        System.Console.WriteLine();
        System.Console.Write("[Press Enter to return] ");
        System.Console.ReadLine();
    }

    private List<Position> CalculatePossibleMoves(Piece piece)
    {
        var moves = new List<Position>();
        var fromPos = piece.Position!;

        // Determine valid directions based on movement type
        var deltas = piece.MovementType switch
        {
            MovementType.Orthogonal => new[] { (-1, 0), (1, 0), (0, -1), (0, 1) },
            MovementType.Diagonal   => new[] { (-1, -1), (-1, 1), (1, -1), (1, 1) },
            MovementType.Jump       => GetJumpDeltas(),
            _                       => new[] { (-1, 0), (1, 0), (0, -1), (0, 1),
                                               (-1, -1), (-1, 1), (1, -1), (1, 1) }
        };

        foreach (var (dr, dc) in deltas)
        {
            for (var dist = 1; dist <= piece.MaxDistance; dist++)
            {
                var newRow = fromPos.Row + (dr * dist);
                var newCol = fromPos.Col + (dc * dist);

                // Bounds check
                if (newRow < 0 || newRow >= Board.Size || newCol < 0 || newCol >= Board.Size)
                    break; // Stop in this direction

                var target = new Position(newRow, newCol);

                // Check passability
                try
                {
                    if (!game.Board.IsPassable(fromPos, target))
                        break;
                }
                catch
                {
                    break;
                }

                // Check occupancy
                if (piece.MovementType == MovementType.Jump)
                {
                    // Jump can't end on occupied tile
                    if (game.Board.GetTile(target).AsPiece is null)
                        moves.Add(target);
                    // Jump doesn't stop at obstacles, continue
                }
                else
                {
                    // Normal movement (Orthogonal, Diagonal)
                    if (game.Board.GetTile(target).AsPiece is not null)
                        break; // Blocked by piece

                    moves.Add(target);
                }
            }
        }

        return moves;
    }

    private (int, int)[] GetJumpDeltas()
    {
        // Jump can go to any direction (8 adjacent + farther)
        var deltas = new List<(int, int)>();
        for (var dr = -1; dr <= 1; dr++)
        for (var dc = -1; dc <= 1; dc++)
            if (dr != 0 || dc != 0)
                deltas.Add((dr, dc));
        return deltas.ToArray();
    }

    private string GetMovementIndicators(Piece piece)
    {
        return piece.MovementType switch
        {
            MovementType.Orthogonal => "↑↓←→",
            MovementType.Diagonal   => "↖↗↙↘",
            MovementType.Jump       => "⤴",
            _                       => "?"
        };
    }

    private void PrintBoardWithCoordinates()
    {
        System.Console.WriteLine("      0     1     2     3     4     5     6     7");
        System.Console.WriteLine("   ┌─────┬─────┬─────┬─────┬─────┬─────┬─────┬─────┐");
        for (var row = 0; row < Board.Size; row++)
        {
            System.Console.Write($" {row} │");
            for (var col = 0; col < Board.Size; col++)
            {
                var pos = new Position(row, col);
                var tile = game.Board.GetTile(pos);
                string cell;
                if (game.Board.IsObstacleCovering(pos))
                    cell = "█████";
                else if (tile.AsPiece is { } piece)
                    cell = piece.PlayerId == playerOneId
                        ? $" 1:{piece.Name[..2]} "
                        : $" 2:{piece.Name[..2]} ";
                else if (tile.AsCoin is { } coin)
                    cell = coin.CoinType == CoinType.Gold ? "  $G " : "  $  ";
                else
                    cell = "     ";
                System.Console.Write(cell + "│");
            }
            System.Console.WriteLine($" {row}");
            if (row < Board.Size - 1)
                System.Console.WriteLine("   ├─────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┤");
        }
        System.Console.WriteLine("   └─────┴─────┴─────┴─────┴─────┴─────┴─────┴─────┘");
        System.Console.WriteLine("      0     1     2     3     4     5     6     7");
    }

    private void PrintBoardWithHighlights(Position piecePos, List<Position> possibleMoves)
    {
        System.Console.WriteLine("      0     1     2     3     4     5     6     7");
        System.Console.WriteLine("   ┌─────┬─────┬─────┬─────┬─────┬─────┬─────┬─────┐");
        for (var row = 0; row < Board.Size; row++)
        {
            System.Console.Write($" {row} │");
            for (var col = 0; col < Board.Size; col++)
            {
                var pos = new Position(row, col);
                var tile = game.Board.GetTile(pos);

                // Determine cell content
                string content;
                if (pos.Equals(piecePos))
                    content = "🟦";
                else if (possibleMoves.Contains(pos))
                    content = "🟩";
                else if (game.Board.IsObstacleCovering(pos) || tile.AsPiece is not null)
                    content = "🟥";
                else if (tile.AsCoin is { } coin)
                    content = coin.CoinType == CoinType.Gold ? "$G" : "$ ";
                else
                    content = "  ";

                System.Console.Write($"  {content}  │");
            }
            System.Console.WriteLine($" {row}");
            if (row < Board.Size - 1)
                System.Console.WriteLine("   ├─────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┤");
        }
        System.Console.WriteLine("   └─────┴─────┴─────┴─────┴─────┴─────┴─────┴─────┘");
        System.Console.WriteLine("      0     1     2     3     4     5     6     7");
    }
}
