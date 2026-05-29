using ScrambleCoin.StarterBot.Models;

namespace ScrambleCoin.StarterBot;

/// <summary>
/// Main game loop: join → poll → decide → submit → repeat until the game ends.
/// </summary>
public sealed class GameLoop
{
    private readonly BotClient _client;
    private readonly IStrategy _strategy;
    private readonly string _botName;

    // How long to wait between state polls
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    // How long to wait between matchmaking queue polls
    private static readonly TimeSpan QueuePollInterval = TimeSpan.FromSeconds(2);

    public GameLoop(BotClient client, IStrategy strategy, string botName)
    {
        _client = client;
        _strategy = strategy;
        _botName = botName;
    }

    // ── Entry point ───────────────────────────────────────────────────────────

    /// <summary>
    /// Runs the bot for a single game.
    /// Call this once per game; it returns when the game ends or the token is cancelled.
    /// </summary>
    public async Task RunAsync(Guid gameId, Guid playerId, CancellationToken ct = default)
    {
        Console.WriteLine($"[{_botName}] Game started — GameId: {gameId} | PlayerId: {playerId}");
        Console.WriteLine("──────────────────────────────────────────────────────────");

        // Track which placement actions we've already submitted this turn
        // to avoid re-submitting after a successful placement.
        var placedThisTurn = new HashSet<Guid>();
        var movedThisTurn = new HashSet<Guid>();
        var lastTurn = -1;
        var lastPhase = "";
        var waitingForOpponentPolls = 0; // counts silent polls while waiting for opponent to place

        while (!ct.IsCancellationRequested)
        {
            BoardState? state;
            try
            {
                state = await _client.GetStateAsync(gameId, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[ERROR] Connection failed: {ex.Message} — retrying in 3 s…");
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
                continue;
            }

            if (state is null)
            {
                await Task.Delay(PollInterval, ct);
                continue;
            }

            // Reset per-turn tracking when a new turn starts
            if (state.Turn != lastTurn)
            {
                placedThisTurn.Clear();
                movedThisTurn.Clear();
                waitingForOpponentPolls = 0;
                lastTurn = state.Turn;
                Console.WriteLine();
                Console.WriteLine($"[Turn {state.Turn}] Phase: {state.Phase ?? "(waiting/ended)"} | Score: {state.YourScore} vs {state.OpponentScore}");
                lastPhase = state.Phase ?? "";
            }
            else if (state.Phase != lastPhase)
            {
                waitingForOpponentPolls = 0;
                Console.WriteLine();
                Console.WriteLine($"[Turn {state.Turn}] Phase: {state.Phase ?? "(waiting/ended)"} | Score: {state.YourScore} vs {state.OpponentScore}");
                lastPhase = state.Phase ?? "";
            }

            // ── Game isn't yet started ──────────────────────────────────────────
            if (state.Phase is null && state.Turn == 0)
            {
                Console.WriteLine("  Waiting for game to start…");
                await Task.Delay(PollInterval, ct);
                continue;
            }

            // ── Game ended ────────────────────────────────────────────────────
            if (state.Phase is null && state.Turn > 0)
            {
                await PrintFinalResultAsync(gameId, playerId, state, ct);
                return;
            }

            // ── CoinSpawn phase ───────────────────────────────────────────────
            if (state.Phase == "CoinSpawn")
            {
                Console.WriteLine("  CoinSpawn — waiting for coins to be placed…");
                await Task.Delay(PollInterval, ct);
                continue;
            }

            // ── PlacePhase ────────────────────────────────────────────────────
            if (state.Phase == "PlacePhase")
            {
                if (placedThisTurn.Any())
                {
                    waitingForOpponentPolls++;
                    if (waitingForOpponentPolls % 5 == 1)
                        Console.WriteLine($"  ⏳ Waiting for opponent to place… ({waitingForOpponentPolls}s)");
                    await Task.Delay(PollInterval, ct);
                    continue;
                }
                await HandlePlacePhaseAsync(gameId, state, placedThisTurn, ct);
                await Task.Delay(PollInterval, ct);
                continue;
            }

            // ── MovePhase ─────────────────────────────────────────────────────
            if (state.Phase == "MovePhase")
            {
                await HandleMovePhaseAsync(gameId, playerId, state, movedThisTurn, ct);
                await Task.Delay(PollInterval, ct);
                continue;
            }

            // Unknown phase — just wait
            await Task.Delay(PollInterval, ct);
        }
    }

    // ── Phase handlers ────────────────────────────────────────────────────────

    private const int MaxPiecesOnBoard = 3;

    private async Task HandlePlacePhaseAsync(
        Guid gameId,
        BoardState state,
        HashSet<Guid> placedThisTurn,
        CancellationToken ct)
    {
        // Already submitted a placement action this turn — caller handles waiting log
        if (placedThisTurn.Any())
            return;

        var piecesOnBoard = state.YourPieces.Count(p => p.IsOnBoard);

        // Already at the 3-piece limit — must skip, cannot place another
        if (piecesOnBoard >= MaxPiecesOnBoard)
        {
            Console.WriteLine($"  → Already at max pieces ({MaxPiecesOnBoard}) on board — skipping placement");
            var r = await _client.SkipPlacementAsync(gameId, ct);
            if (r is not null)
            {
                placedThisTurn.Add(Guid.Empty); // sentinel: marks that we have acted this turn
                Console.WriteLine($"  ✓ Placement skipped. Phase after: {r.Phase ?? "ended"}");
            }
            return;
        }

        var unplaced = state.YourPieces.Where(p => !p.IsOnBoard).ToList();

        // No pieces left in hand (shouldn't normally happen, but be safe)
        if (unplaced.Count == 0)
        {
            Console.WriteLine("  → No pieces in hand — skipping placement");
            var r = await _client.SkipPlacementAsync(gameId, ct);
            if (r is not null)
            {
                placedThisTurn.Add(Guid.Empty);
                Console.WriteLine($"  ✓ Placement skipped. Phase after: {r.Phase ?? "ended"}");
            }
            return;
        }

        foreach (var piece in unplaced)
        {
            if (ct.IsCancellationRequested) break;

            Console.WriteLine($"  Placing piece: {piece.Name} ({piece.PieceId})");
            var decision = _strategy.DecidePlacement(state, piece);

            var result = decision switch
            {
                PlacementDecision.Place place => await _client.PlacePieceAsync(gameId, place.PieceId, place.Position, ct),
                PlacementDecision.Skip        => await _client.SkipPlacementAsync(gameId, ct),
                _                             => null
            };

            if (result is not null)
            {
                placedThisTurn.Add(piece.PieceId);
                Console.WriteLine($"  ✓ Placement submitted. Phase after: {result.Phase ?? "ended"}");
                break; // Only one placement action per player per PlacePhase
            }
            else
            {
                // Placement failed (e.g. tile occupied by opponent who placed concurrently).
                // Stop retrying now — state is stale. The next poll will refresh it.
                break;
            }
        }
    }

    private async Task HandleMovePhaseAsync(
        Guid gameId,
        Guid playerId,
        BoardState state,
        HashSet<Guid> movedThisTurn,
        CancellationToken ct)
    {
        // Only act when it is our turn
        if (state.ActivePlayer is null || !state.ActivePlayer.Equals(playerId.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"  Waiting for our turn… (active: {state.ActivePlayer ?? "none"})");
            return;
        }

        var piecesToMove = state.YourPieces
            .Where(p => p.IsOnBoard && !movedThisTurn.Contains(p.PieceId))
            .ToList();

        if (piecesToMove.Count == 0)
        {
            // All pieces moved this turn
            return;
        }

        // Move one piece at a time (re-poll between each move to get a fresh state)
        var piece = piecesToMove.First();
        Console.WriteLine($"  Moving piece: {piece.Name} at {piece.Position} ({piece.PieceId})");
        var decision = _strategy.DecideMove(state, piece);

        var result = await _client.MovePieceAsync(gameId, decision.PieceId, decision.Segments, ct);

        if (result is not null)
        {
            movedThisTurn.Add(piece.PieceId);
            Console.WriteLine($"  ✓ Move submitted. Phase after: {result.Phase ?? "ended"} | Score: {result.YourScore} vs {result.OpponentScore}");
        }
    }

    // ── Result printing ───────────────────────────────────────────────────────

    private async Task PrintFinalResultAsync(Guid gameId, Guid playerId, BoardState lastState, CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("══════════════════════════════════════════════════════════");
        Console.WriteLine("GAME OVER");

        GameResult? result = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            result = await _client.GetResultAsync(gameId, ct);
            if (result is not null) break;
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }

        if (result is null)
        {
            // Fall back to last known state scores
            Console.WriteLine($"Final score (from last state): {lastState.YourScore} vs {lastState.OpponentScore}");
        }
        else
        {
            var yourScore  = result.PlayerOneId == playerId ? result.PlayerOneScore : result.PlayerTwoScore;
            var theirScore = result.PlayerOneId == playerId ? result.PlayerTwoScore : result.PlayerOneScore;

            string outcome;
            if (result.IsDraw)
                outcome = "DRAW";
            else if (result.WinnerId == playerId)
                outcome = "WIN 🎉";
            else
                outcome = "LOSS";

            Console.WriteLine($"Result:     {outcome}");
            Console.WriteLine($"Your score: {yourScore}");
            Console.WriteLine($"Opponent:   {theirScore}");
            Console.WriteLine($"Game ID:    {result.GameId}");
        }

        Console.WriteLine("══════════════════════════════════════════════════════════");
    }

    // ── Matchmaking ───────────────────────────────────────────────────────────

    /// <summary>
    /// Uses the matchmaking queue to find a 1v1 opponent.
    /// Returns the assigned game ID, player ID, and bot token once matched.
    /// </summary>
    public async Task<(Guid GameId, Guid PlayerId, Guid Token)?> JoinViaQueueAsync(
        IReadOnlyList<string> lineup,
        CancellationToken ct = default)
    {
        Console.WriteLine("Joining matchmaking queue…");
        var queueResponse = await _client.EnqueueAsync(lineup, ct);
        if (queueResponse is null) return null;

        // Matched immediately — server returns { gameId, playerId, token } with no "status" field
        if (queueResponse is { GameId: not null, PlayerId: not null, Token: not null })
        {
            Console.WriteLine($"Matched immediately! GameId: {queueResponse.GameId}");
            return (queueResponse.GameId.Value, queueResponse.PlayerId.Value, queueResponse.Token.Value);
        }

        // Waiting — poll until matched
        if (queueResponse is { Status: "waiting", QueueId: not null })
        {
            var queueId = queueResponse.QueueId.Value;
            Console.WriteLine($"Waiting in queue (QueueId: {queueId})…");

            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(QueuePollInterval, ct);
                var poll = await _client.PollQueueAsync(queueId, ct);
                if (poll is null) continue;

                Console.WriteLine($"  Queue status: {poll.Status}");

                if (poll is { Status: "matched", GameId: not null, PlayerId: not null, Token: not null })
                {
                    Console.WriteLine($"Matched! GameId: {poll.GameId}");
                    return (poll.GameId.Value, poll.PlayerId.Value, poll.Token.Value);
                }

                if (poll.Status == "timed_out")
                {
                    Console.WriteLine("Queue timed out — no opponent found. Restart the bot to try again.");
                    return null;
                }
            }
        }

        return null;
    }
}
