using Microsoft.AspNetCore.SignalR.Client;
using System.Threading.Channels;
using ScrambleCoin.StarterBot.Models;

namespace ScrambleCoin.StarterBot;

/// <summary>
/// Main game loop: join → react to SignalR events → decide → submit → repeat until the game ends.
/// Replaces the old polling loop with a push-driven model: the server notifies the bot whenever
/// the board state changes, so the bot only calls REST when it actually needs to act.
/// </summary>
public sealed class GameLoop
{
    private readonly BotClient _client;
    private readonly IStrategy _strategy;
    private readonly string _botName;

    // Fallback: if no SignalR event arrives within this window, do a one-off REST poll
    // to recover from a potentially missed event (e.g. after a brief reconnection).
    private static readonly TimeSpan FallbackPollTimeout = TimeSpan.FromSeconds(5);

    // How long to wait between matchmaking queue polls (unchanged)
    private static readonly TimeSpan QueuePollInterval = TimeSpan.FromSeconds(2);

    public GameLoop(BotClient client, IStrategy strategy, string botName)
    {
        _client = client;
        _strategy = strategy;
        _botName = botName;
    }

    // ── Entry point ───────────────────────────────────────────────────────────

    /// <summary>
    /// Runs the bot for a single game via SignalR push events.
    /// Returns when the game ends or the token is cancelled.
    /// </summary>
    public async Task RunAsync(Guid gameId, Guid playerId, CancellationToken ct = default)
    {
        Console.WriteLine($"[{_botName}] Game started — GameId: {gameId} | PlayerId: {playerId}");
        Console.WriteLine("──────────────────────────────────────────────────────────");

        // ── SignalR connection ─────────────────────────────────────────────────
        var hub = new HubConnectionBuilder()
            .WithUrl(_client.HubUrl)
            .WithAutomaticReconnect()
            .Build();

        // Channel bridges SignalR callbacks (background thread) into the async loop below.
        var events = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });

        // ActionRequired is sent only to THIS player's private group — zero noise from opponent actions.
        hub.On("ActionRequired", (object _) => Push("action-required"));
        hub.On("GameEnded",      (object _) => Push("game-ended"));

        hub.Reconnected += async _ =>
        {
            Push("reconnected");
            // Re-join groups — auto-reconnect creates a new connection that has left all groups.
            try
            {
                await hub.InvokeAsync("JoinGame",         gameId.ToString(), cancellationToken: ct);
                await hub.InvokeAsync("RegisterAsPlayer", gameId.ToString(),   playerId.ToString(), cancellationToken: ct);
            }
            catch { /* best-effort: may fail if the hub is still settling */ }
        };
        hub.Closed      += ex =>
        {
            Console.WriteLine($"[{_botName}]  ⚠ SignalR disconnected: {ex?.Message}");
            Push("disconnected");
            return Task.CompletedTask;
        };

        try
        {
            await hub.StartAsync(ct);
            // Join the public spectator group (for GameEnded)
            await hub.InvokeAsync("JoinGame", gameId.ToString(), ct);
            // Join the private player group (for ActionRequired — only our turn notifications)
            await hub.InvokeAsync("RegisterAsPlayer", gameId.ToString(), playerId.ToString(), ct);
            Console.WriteLine($"[{_botName}]  🔔 SignalR connected — waiting for ActionRequired events…");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{_botName}]  ⚠ SignalR unavailable ({ex.Message}) — falling back to polling.");
            await hub.DisposeAsync();
            await RunPollingFallbackAsync(gameId, playerId, ct);
            return;
        }

        // Track which actions we've already submitted this turn
        var placedThisTurn = new HashSet<Guid>();
        var movedThisTurn  = new HashSet<Guid>();
        var lastTurn       = -1;
        var lastPhase      = "";

        // Seed the channel so we do an immediate state fetch on startup.
        Push("init");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Wait for the next event, but time out to handle missed events.
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(FallbackPollTimeout);

                try
                {
                    await events.Reader.ReadAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    // Timeout — no event in 5 s; poll once as a safety net.
                    Console.WriteLine($"[{_botName}]  ⏱ No SignalR event for 5 s — polling once.");
                    // Re-register in case a reconnection silently dropped us from the player group.
                    try
                    {
                        await hub.InvokeAsync("RegisterAsPlayer", gameId.ToString(), playerId.ToString(), ct);
                    }
                    catch { /* best-effort: hub may be reconnecting */ }
                }

                // Fetch fresh player-specific state via REST (the SignalR payload is spectator-only).
                BoardState? state;
                try
                {
                    state = await _client.GetStateAsync(gameId, ct);
                }
                catch (OperationCanceledException) { break; }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"[{_botName}]  [ERROR] Connection failed: {ex.Message}");
                    continue;
                }

                if (state is null) continue;

                // ── Turn / phase tracking ──────────────────────────────────────
                if (state.Turn != lastTurn)
                {
                    placedThisTurn.Clear();
                    movedThisTurn.Clear();
                    lastTurn = state.Turn;
                    Console.WriteLine();
                    Console.WriteLine($"[{_botName}] [Turn {state.Turn}] Phase: {state.Phase ?? "(waiting/ended)"} | Score: {state.YourScore} vs {state.OpponentScore}");
                    lastPhase = state.Phase ?? "";
                }
                else if (state.Phase != lastPhase)
                {
                    Console.WriteLine();
                    Console.WriteLine($"[{_botName}] [Turn {state.Turn}] Phase: {state.Phase ?? "(waiting/ended)"} | Score: {state.YourScore} vs {state.OpponentScore}");
                    lastPhase = state.Phase ?? "";
                }

                switch (state.Phase)
                {
                    // ── Game isn't started yet ───────────────────────────────────────
                    case null when state.Turn == 0:
                        Console.WriteLine($"[{_botName}]  Waiting for game to start…");
                        continue; // wait for the next SignalR event
                    // ── Game ended ────────────────────────────────────────────────
                    case null when state.Turn > 0:
                        await PrintFinalResultAsync(gameId, playerId, state, ct);
                        return;
                    // ── CoinSpawn ─────────────────────────────────────────────────
                    case "CoinSpawn":
                        // Server handles this; the next SignalR event will show PlacePhase.
                        continue;
                    // ── PlacePhase ────────────────────────────────────────────────
                    case "PlacePhase":
                        {
                            if (placedThisTurn.Count == 0)
                                await HandlePlacePhaseAsync(gameId, state, placedThisTurn, ct);
                            // If already placed, just wait for the next event (opponent placing).
                            continue;
                        }

                    // ── MovePhase ─────────────────────────────────────────────────
                    case "MovePhase":
                        await HandleMovePhaseAsync(gameId, playerId, state, movedThisTurn, ct);
                        break;
                }
            }
        }
        finally
        {
            await hub.StopAsync(CancellationToken.None);
            await hub.DisposeAsync();
        }
        return;

        void Push(string reason) => events.Writer.TryWrite(reason);
    }

    // ── Phase handlers ────────────────────────────────────────────────────────

    private const int MaxPiecesOnBoard = 3;

    private async Task HandlePlacePhaseAsync(
        Guid gameId,
        BoardState state,
        HashSet<Guid> placedThisTurn,
        CancellationToken ct)
    {
        if (placedThisTurn.Count != 0) return;

        var piecesOnBoard = state.YourPieces.Count(p => p.IsOnBoard);

        if (piecesOnBoard >= MaxPiecesOnBoard)
        {
            Console.WriteLine($"[{_botName}]  → Already at max pieces ({MaxPiecesOnBoard}) on board — skipping placement");
            var r = await _client.SkipPlacementAsync(gameId, ct);
            if (r is null)
            {
                return;
            }
            placedThisTurn.Add(Guid.Empty);
            Console.WriteLine($"[{_botName}]  ✓ Placement skipped. Phase after: {r.Phase ?? "ended"}");
            return;
        }

        var unplaced = state.YourPieces.Where(p => !p.IsOnBoard).ToList();

        if (unplaced.Count == 0)
        {
            Console.WriteLine($"[{_botName}]  → No pieces in hand — skipping placement");
            var r = await _client.SkipPlacementAsync(gameId, ct);
            if (r is null)
            {
                return;
            }
            placedThisTurn.Add(Guid.Empty);
            Console.WriteLine($"[{_botName}]  ✓ Placement skipped. Phase after: {r.Phase ?? "ended"}");
            return;
        }

        var currentState = state;
        var piece        = unplaced.First();
        const int maxRetries = 5;

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            if (ct.IsCancellationRequested) break;

            Console.WriteLine($"[{_botName}]  Placing piece: {piece.Name} ({piece.PieceId})");
            var decision = _strategy.DecidePlacement(currentState, piece);

            var result = decision switch
            {
                PlacementDecision.Place place => await _client.PlacePieceAsync(gameId, place.PieceId, place.Position, ct),
                PlacementDecision.Skip        => await _client.SkipPlacementAsync(gameId, ct),
                _                             => null
            };

            if (result is not null)
            {
                placedThisTurn.Add(piece.PieceId);
                Console.WriteLine($"[{_botName}]  ✓ Placement submitted. Phase after: {result.Phase ?? "ended"}");
                return;
            }

            Console.WriteLine($"[{_botName}]  ↩ Placement failed, refreshing state (attempt {attempt + 1}/{maxRetries})…");
            await Task.Delay(TimeSpan.FromMilliseconds(200), ct);
            currentState = await _client.GetStateAsync(gameId, ct) ?? currentState;
        }
    }

    private async Task HandleMovePhaseAsync(
        Guid gameId,
        Guid playerId,
        BoardState state,
        HashSet<Guid> movedThisTurn,
        CancellationToken ct)
    {
        if (state.ActivePlayer is null ||
            !state.ActivePlayer.Equals(playerId.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"[{_botName}]  Waiting for our turn… (active: {state.ActivePlayer ?? "none"})");
            return;
        }

        var piecesToMove = state.YourPieces
            .Where(p => p.IsOnBoard && !movedThisTurn.Contains(p.PieceId))
            .ToList();

        if (piecesToMove.Count == 0) return;

        var piece    = piecesToMove.First();
        var decision = _strategy.DecideMove(state, piece);
        Console.WriteLine($"[{_botName}]  Moving piece: {piece.Name} at {piece.Position} ({piece.PieceId})");

        var result = await _client.MovePieceAsync(gameId, decision.PieceId, decision.Segments, ct);
        if (result is not null)
        {
            movedThisTurn.Add(piece.PieceId);
            Console.WriteLine($"[{_botName}]  ✓ Move submitted. Phase after: {result.Phase ?? "ended"} | Score: {result.YourScore} vs {result.OpponentScore}");
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
            Console.WriteLine($"[{_botName}] Final score (from last state): {lastState.YourScore} vs {lastState.OpponentScore}");
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

    // ── Polling fallback ──────────────────────────────────────────────────────

    /// <summary>
    /// Legacy polling loop used when SignalR is unavailable.
    /// Identical in behaviour to the old <c>GameLoop</c> but without the 1-second fixed wait.
    /// </summary>
    private async Task RunPollingFallbackAsync(Guid gameId, Guid playerId, CancellationToken ct)
    {
        var placedThisTurn = new HashSet<Guid>();
        var movedThisTurn  = new HashSet<Guid>();
        var lastTurn       = -1;
        var lastPhase      = "";

        while (!ct.IsCancellationRequested)
        {
            BoardState? state;
            try { state = await _client.GetStateAsync(gameId, ct); }
            catch (OperationCanceledException) { break; }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[{_botName}] [ERROR] {ex.Message} — retrying in 3 s…");
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
                continue;
            }

            if (state is null) { await Task.Delay(TimeSpan.FromSeconds(1), ct); continue; }

            if (state.Turn != lastTurn)
            {
                placedThisTurn.Clear();
                movedThisTurn.Clear();
                lastTurn = state.Turn;
                Console.WriteLine();
                Console.WriteLine($"[{_botName}] [Turn {state.Turn}] Phase: {state.Phase ?? "(waiting/ended)"} | Score: {state.YourScore} vs {state.OpponentScore}");
                lastPhase = state.Phase ?? "";
            }
            else if (state.Phase != lastPhase)
            {
                Console.WriteLine();
                Console.WriteLine($"[{_botName}] [Turn {state.Turn}] Phase: {state.Phase ?? "(waiting/ended)"} | Score: {state.YourScore} vs {state.OpponentScore}");
                lastPhase = state.Phase ?? "";
            }

            switch (state.Phase)
            {
                case null when state.Turn == 0:
                    break;
                case null when state.Turn > 0:
                    await PrintFinalResultAsync(gameId, playerId, state, ct); return;
                case "CoinSpawn":
                    break;
                case "PlacePhase":
                    {
                        if (placedThisTurn.Count == 0)
                            await HandlePlacePhaseAsync(gameId, state, placedThisTurn, ct);
                        break;
                    }

                case "MovePhase":
                    await HandleMovePhaseAsync(gameId, playerId, state, movedThisTurn, ct);
                    break;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }
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
        switch (queueResponse)
        {
            case null:
                break;
            case { GameId: not null, PlayerId: not null, Token: not null }:
                Console.WriteLine($"Matched immediately! GameId: {queueResponse.GameId}");
                return (queueResponse.GameId.Value, queueResponse.PlayerId.Value, queueResponse.Token.Value);
            case { Status: "waiting", QueueId: not null }:
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

                        if (poll.Status != "timed_out")
                        {
                            continue;
                        }
                        Console.WriteLine("Queue timed out — no opponent found. Restart the bot to try again.");
                        return null;
                    }
                    break;
                }
        }

        return null;
    }
}
