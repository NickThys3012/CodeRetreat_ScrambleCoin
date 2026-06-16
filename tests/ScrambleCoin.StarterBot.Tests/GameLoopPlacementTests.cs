using System.Net;
using System.Text.Json;
using ScrambleCoin.StarterBot.Models;

namespace ScrambleCoin.StarterBot.Tests;

/// <summary>
/// Behavioural tests for <see cref="GameLoop"/> placement logic.
/// Uses a custom <see cref="FakeHttpMessageHandler"/> to intercept HTTP calls
/// so no real server is needed.
///
/// Key invariant under test:
///   After submitting a successful placement, the GameLoop must NOT attempt to
///   place a second piece in the same PlacePhase round (the <c>break</c> fix).
/// </summary>
public sealed class GameLoopPlacementTests
{
    private static readonly Guid PlayerId = Guid.NewGuid();
    private static readonly Guid GameId   = Guid.NewGuid();

    /// <summary>
    /// Returns a <see cref="GameResult"/> response so <c>PrintFinalResultAsync</c>
    /// exits immediately without entering its retry-with-delay loop.
    /// </summary>
    private static HttpResponseMessage GameResultOk() =>
        JsonResponse(new GameResult
        {
            GameId         = GameId,
            Status         = "finished",
            PlayerOneId    = PlayerId,
            PlayerOneScore = 5,
            PlayerTwoId    = Guid.NewGuid(),
            PlayerTwoScore = 3,
            WinnerId       = PlayerId,
            IsDraw         = false
        });

    // ── Test: only one placement per PlacePhase poll ──────────────────────────

    [Fact]
    public async Task HandlePlacePhase_WithTwoUnplacedPieces_OnlySubmitsOnePlacement()
    {
        var pieceA = new PieceState { PieceId = Guid.NewGuid(), Name = "Mickey", IsOnBoard = false, MovesPerTurn = 1, MovementType = "Orthogonal" };
        var pieceB = new PieceState { PieceId = Guid.NewGuid(), Name = "Minnie", IsOnBoard = false, MovesPerTurn = 1, MovementType = "Orthogonal" };

        var placeCallCount = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var handler = new FakeHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;

            // GET /api/games/{id}/state — return PlacePhase state with two unplaced pieces
            if (request.Method == HttpMethod.Get && path.EndsWith("/state"))
            {
                // After the first placement is recorded, switch to ended so RunAsync exits
                var phase = placeCallCount == 0 ? "PlacePhase" : null;

                return JsonResponse(new BoardState
                {
                    Turn       = 1,
                    Phase      = phase,
                    YourPieces = [pieceA, pieceB],
                    Board      = new BoardData()
                });
            }

            // POST /api/games/{id}/place — record the call and return success
            if (request.Method == HttpMethod.Post && path.EndsWith("/place"))
            {
                Interlocked.Increment(ref placeCallCount);
                return JsonResponse(new PlacementResponse { Phase = "PlacePhase" });
            }

            // GET /api/games/{id}/result — return immediately so RunAsync exits cleanly
            if (request.Method == HttpMethod.Get && path.EndsWith("/result"))
                return GameResultOk();

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://fake-server/")
        };

        var botClient = new BotClient(httpClient);
        var strategy  = new GreedyStrategy();
        var gameLoop  = new GameLoop(botClient, strategy, botName: "TestBot");

        // RunAsync exits once it detects phase == null && turn > 0 (game ended)
        await gameLoop.RunAsync(GameId, PlayerId, cts.Token);

        // ── Assertion ──────────────────────────────────────────────────────────
        // Even though there were TWO unplaced pieces, only ONE placement call
        // should have been made (the `break` after the first successful placement).
        Assert.Equal(1, placeCallCount);
    }

    [Fact]
    public async Task HandlePlacePhase_AfterSuccessfulPlacement_SecondPieceIsNotSubmittedInSamePoll()
    {
        // Arrange: three unplaced pieces in the initial state
        var pieces = Enumerable.Range(0, 3)
            .Select(_ => new PieceState
            {
                PieceId      = Guid.NewGuid(),
                Name         = "Mickey",
                IsOnBoard    = false,
                MovesPerTurn = 1,
                MovementType = "Orthogonal"
            })
            .ToList();

        var placeCallCount = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var handler = new FakeHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;

            if (request.Method == HttpMethod.Get && path.EndsWith("/state"))
            {
                var phase = placeCallCount == 0 ? "PlacePhase" : null;
                return JsonResponse(new BoardState
                {
                    Turn       = 1,
                    Phase      = phase,
                    YourPieces = pieces,
                    Board      = new BoardData()
                });
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/place"))
            {
                Interlocked.Increment(ref placeCallCount);
                return JsonResponse(new PlacementResponse { Phase = "PlacePhase" });
            }

            if (request.Method == HttpMethod.Get && path.EndsWith("/result"))
                return GameResultOk();

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://fake-server/") };
        var botClient  = new BotClient(httpClient);
        var gameLoop   = new GameLoop(botClient, new GreedyStrategy(), "TestBot");

        await gameLoop.RunAsync(GameId, PlayerId, cts.Token);

        // Regardless of how many unplaced pieces there were, the loop breaks
        // after the first successful placement in a single PlacePhase poll.
        Assert.Equal(1, placeCallCount);
    }

    // ── Test: pieces already placed are not re-submitted ─────────────────────

    [Fact]
    public async Task HandlePlacePhase_PieceThatIsAlreadyOnBoard_IsNotSubmittedForPlacement()
    {
        var onBoardPiece  = new PieceState { PieceId = Guid.NewGuid(), Name = "Mickey",  IsOnBoard = true,  Position = new Position(0, 1), MovesPerTurn = 1, MovementType = "Orthogonal" };
        var offBoardPiece = new PieceState { PieceId = Guid.NewGuid(), Name = "Minnie",  IsOnBoard = false, MovesPerTurn = 1, MovementType = "Orthogonal" };

        var placeCallCount = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var handler = new FakeHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;

            if (request.Method == HttpMethod.Get && path.EndsWith("/state"))
            {
                var phase = placeCallCount == 0 ? "PlacePhase" : null;
                return JsonResponse(new BoardState
                {
                    Turn       = 1,
                    Phase      = phase,
                    YourPieces = [onBoardPiece, offBoardPiece],
                    Board      = new BoardData()
                });
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/place"))
            {
                Interlocked.Increment(ref placeCallCount);
                return JsonResponse(new PlacementResponse { Phase = "PlacePhase" });
            }

            if (request.Method == HttpMethod.Get && path.EndsWith("/result"))
                return GameResultOk();

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://fake-server/") };
        var botClient  = new BotClient(httpClient);
        var gameLoop   = new GameLoop(botClient, new GreedyStrategy(), "TestBot");

        await gameLoop.RunAsync(GameId, PlayerId, cts.Token);

        // Only the off-board piece should have been placed (exactly once)
        Assert.Equal(1, placeCallCount);
    }

    // ── Test: game-not-yet-started state is handled without crashing ──────────

    [Fact]
    public async Task RunAsync_WhenPhaseIsNullAndTurnIsZero_PollsStateUntilCancelled()
    {
        var callCount = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        var handler = new FakeHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;

            if (request.Method != HttpMethod.Get || !path.EndsWith("/state"))
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            
            callCount++;
            // Phase = null, Turn = 0 → "waiting for game to start"
            return JsonResponse(new BoardState { Turn = 0, Phase = null });

        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://fake-server/") };
        var botClient  = new BotClient(httpClient);
        var gameLoop   = new GameLoop(botClient, new GreedyStrategy(), "TestBot");

        // RunAsync loops until the CancellationToken fires.
        // Task.Delay inside the loop throws OperationCanceledException — that is
        // the expected termination path; catch it here so the test stays green.
        try
        {
            await gameLoop.RunAsync(GameId, PlayerId, cts.Token);
        }
        catch (OperationCanceledException) { /* normal loop exit via Task.Delay cancellation */ }

        // The loop polled state at least once before being cancelled
        Assert.True(callCount >= 1, "Expected at least one state poll while waiting for start");
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

/// <summary>
/// Minimal <see cref="HttpMessageHandler"/> that delegates every request to a
/// caller-supplied <see cref="Func{T,TResult}"/>.  No real network calls are made.
/// </summary>
internal sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken  cancellationToken)
        => Task.FromResult(handler(request));
}
