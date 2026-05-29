using System.Net;
using System.Text.Json;
using ScrambleCoin.StarterBot.Models;

namespace ScrambleCoin.StarterBot.Tests;

/// <summary>
/// Behavioural tests for <see cref="GameLoop"/> move-phase logic.
/// Uses <see cref="FakeHttpMessageHandler"/> (defined in GameLoopPlacementTests.cs)
/// to intercept HTTP calls so no real server is needed.
/// </summary>
public sealed class GameLoopMoveTests
{
    private static readonly Guid PlayerId      = Guid.NewGuid();
    private static readonly Guid OtherPlayerId = Guid.NewGuid();
    private static readonly Guid GameId        = Guid.NewGuid();

    private static HttpResponseMessage GameResultOk() =>
        JsonResponse(new GameResult
        {
            GameId         = GameId,
            Status         = "finished",
            PlayerOneId    = PlayerId,
            PlayerOneScore = 5,
            PlayerTwoId    = OtherPlayerId,
            PlayerTwoScore = 3,
            WinnerId       = PlayerId,
            IsDraw         = false
        });

    // ── Test: active player is not us → no move submitted ────────────────────

    [Fact]
    public async Task HandleMovePhase_WhenActivePlayerIsNotUs_DoesNotSubmitMove()
    {
        // Arrange: a placed piece belonging to us
        var ourPiece = new PieceState
        {
            PieceId      = Guid.NewGuid(),
            Name         = "Mickey",
            IsOnBoard    = true,
            Position     = new Position(3, 3),
            MovesPerTurn = 1,
            MovementType = "Orthogonal"
        };

        var moveCallCount = 0;
        var statePollCount = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var handler = new FakeHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;

            if (request.Method == HttpMethod.Get && path.EndsWith("/state"))
            {
                statePollCount++;

                // First poll: MovePhase with a DIFFERENT active player — we should not move.
                // Second poll: game has ended so RunAsync exits cleanly.
                var (phase, activePid) = statePollCount == 1
                    ? ("MovePhase", OtherPlayerId.ToString())
                    : ((string?)null, (string?)null);

                return JsonResponse(new BoardState
                {
                    Turn         = 1,
                    Phase        = phase,
                    ActivePlayer = activePid,
                    YourPieces   = [ourPiece],
                    Board        = new BoardData()
                });
            }

            // POST /move — should NEVER be called
            if (request.Method == HttpMethod.Post && path.EndsWith("/move"))
            {
                Interlocked.Increment(ref moveCallCount);
                return JsonResponse(new MoveResponse { Phase = "MovePhase" });
            }

            if (request.Method == HttpMethod.Get && path.EndsWith("/result"))
                return GameResultOk();

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://fake-server/") };
        var botClient  = new BotClient(httpClient);
        var gameLoop   = new GameLoop(botClient, new GreedyStrategy(), "TestBot");

        await gameLoop.RunAsync(GameId, PlayerId, cts.Token);

        // ── Assertion ──────────────────────────────────────────────────────────
        // ActivePlayer was someone else, so the GameLoop must not call the move API.
        Assert.Equal(0, moveCallCount);
    }

    // ── Test: piece already moved this turn is not resubmitted on next poll ──

    [Fact]
    public async Task HandleMovePhase_AfterSuccessfulMove_PieceIsNotResubmittedOnNextPoll()
    {
        // Arrange: a placed piece with a coin to move toward
        var ourPiece = new PieceState
        {
            PieceId      = Guid.NewGuid(),
            Name         = "Mickey",
            IsOnBoard    = true,
            Position     = new Position(3, 3),
            MovesPerTurn = 1,
            MovementType = "Orthogonal"
        };
        var coin = new CoinState { Position = new Position(3, 6) };

        var moveCallCount = 0;
        var statePollCount = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var handler = new FakeHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;

            if (request.Method == HttpMethod.Get && path.EndsWith("/state"))
            {
                statePollCount++;

                // Poll 1 & 2: MovePhase, it is our turn — piece is still on board
                // Poll 3+: game ended so the loop exits
                var (phase, activePid) = statePollCount <= 2
                    ? ("MovePhase", PlayerId.ToString())
                    : ((string?)null, (string?)null);

                return JsonResponse(new BoardState
                {
                    Turn           = 1,
                    Phase          = phase,
                    ActivePlayer   = activePid,
                    YourPieces     = [ourPiece],
                    AvailableCoins = [coin],
                    Board          = new BoardData()
                });
            }

            // POST /move — record each call
            if (request.Method == HttpMethod.Post && path.EndsWith("/move"))
            {
                Interlocked.Increment(ref moveCallCount);
                return JsonResponse(new MoveResponse { Phase = "MovePhase", YourScore = 0, OpponentScore = 0 });
            }

            if (request.Method == HttpMethod.Get && path.EndsWith("/result"))
                return GameResultOk();

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://fake-server/") };
        var botClient  = new BotClient(httpClient);
        var gameLoop   = new GameLoop(botClient, new GreedyStrategy(), "TestBot");

        await gameLoop.RunAsync(GameId, PlayerId, cts.Token);

        // ── Assertion ──────────────────────────────────────────────────────────
        // The piece was eligible to move on both poll 1 and poll 2.  However, after
        // the first successful move the piece's ID is tracked in `movedThisTurn`, so
        // the second poll must NOT re-submit the same piece.
        Assert.Equal(1, moveCallCount);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static HttpResponseMessage JsonResponse<T>(T body)
    {
        var json    = JsonSerializer.Serialize(body, JsonOptions);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
