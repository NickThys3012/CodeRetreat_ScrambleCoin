using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ScrambleCoin.StarterBot.Models;

namespace ScrambleCoin.StarterBot;

/// <summary>
/// Thin wrapper around <see cref="HttpClient"/> for all ScrambleCoin bot API calls.
/// All methods print clear error messages for 400/403/409 responses and return
/// <c>null</c> on recoverable errors so the <see cref="GameLoop"/> can retry.
/// </summary>
public sealed class BotClient : IDisposable
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public BotClient(string baseUrl)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + '/') };
    }

    // ── Authentication ────────────────────────────────────────────────────────

    /// <summary>Sets the bot token sent on every subsequent request via <c>X-Bot-Token</c>.</summary>
    public void SetBotToken(Guid token)
    {
        _http.DefaultRequestHeaders.Remove("X-Bot-Token");
        _http.DefaultRequestHeaders.Add("X-Bot-Token", token.ToString());
    }

    // ── Game joining ──────────────────────────────────────────────────────────

    /// <summary>
    /// Joins a specific pre-created game.
    /// <c>POST /api/games/{gameId}/join</c>
    /// </summary>
    public async Task<JoinResponse?> JoinGameAsync(Guid gameId, IReadOnlyList<string> lineup, CancellationToken ct = default)
    {
        var body = new { lineup };
        var response = await _http.PostAsJsonAsync($"api/games/{gameId}/join", body, ct);
        return await ReadResponseAsync<JoinResponse>(response, "JoinGame");
    }

    /// <summary>
    /// Enqueues the bot for matchmaking (1v1).
    /// <c>POST /api/games/queue</c>
    /// Returns immediately with either a <c>matched</c> result (200) or a queue ID to poll (202).
    /// </summary>
    public async Task<QueueResponse?> EnqueueAsync(IReadOnlyList<string> lineup, CancellationToken ct = default)
    {
        var body = new { lineup };
        var response = await _http.PostAsJsonAsync("api/games/queue", body, ct);

        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            // 202 — waiting; body contains { queueId }
            var content = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var queueId = content.GetProperty("queueId").GetGuid();
            return new QueueResponse { Status = "waiting", QueueId = queueId };
        }

        if (response.IsSuccessStatusCode)
        {
            // 200 — matched immediately
            return await response.Content.ReadFromJsonAsync<QueueResponse>(JsonOptions, ct);
        }

        await PrintApiErrorAsync(response, "Enqueue");
        return null;
    }

    /// <summary>
    /// Polls a queue entry for matchmaking status.
    /// <c>GET /api/games/queue/{queueId}</c>
    /// </summary>
    public async Task<QueuePollResponse?> PollQueueAsync(Guid queueId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync(BuildUri($"api/games/queue/{queueId}"), ct);
        if (!response.IsSuccessStatusCode)
        {
            await PrintApiErrorAsync(response, "PollQueue");
            return null;
        }
        return await response.Content.ReadFromJsonAsync<QueuePollResponse>(JsonOptions, ct);
    }

    // ── Game state ────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads the current board state.
    /// <c>GET /api/games/{gameId}/state</c>
    /// </summary>
    public async Task<BoardState?> GetStateAsync(Guid gameId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync(BuildUri($"api/games/{gameId}/state"), ct);
        return await ReadResponseAsync<BoardState>(response, "GetState");
    }

    // ── Placement ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Submits a placement action during PlacePhase.
    /// <c>POST /api/games/{gameId}/place</c>
    /// </summary>
    public async Task<PlacementResponse?> PlacePieceAsync(Guid gameId, Guid pieceId, Position position, CancellationToken ct = default)
    {
        var body = new
        {
            action = "place",
            pieceId,
            position = new { row = position.Row, col = position.Col }
        };
        var response = await _http.PostAsJsonAsync($"api/games/{gameId}/place", body, ct);
        return await ReadResponseAsync<PlacementResponse>(response, "PlacePiece");
    }

    /// <summary>
    /// Submits a skip action during PlacePhase (do not place any piece this turn).
    /// <c>POST /api/games/{gameId}/place</c>
    /// </summary>
    public async Task<PlacementResponse?> SkipPlacementAsync(Guid gameId, CancellationToken ct = default)
    {
        var body = new { action = "skip" };
        var response = await _http.PostAsJsonAsync($"api/games/{gameId}/place", body, ct);
        return await ReadResponseAsync<PlacementResponse>(response, "SkipPlacement");
    }

    // ── Movement ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Submits a piece move during MovePhase.
    /// <c>POST /api/games/{gameId}/move</c>
    /// <para>
    /// <paramref name="segments"/> must contain exactly <c>MovesPerTurn</c> entries.
    /// Each segment is a list of positions the piece steps through (empty = stay still for that action).
    /// </para>
    /// </summary>
    public async Task<MoveResponse?> MovePieceAsync(
        Guid gameId,
        Guid pieceId,
        IReadOnlyList<IReadOnlyList<Position>> segments,
        CancellationToken ct = default)
    {
        var body = new
        {
            pieceId,
            segments = segments.Select(seg =>
                seg.Select(p => new { row = p.Row, col = p.Col }).ToList()
            ).ToList()
        };
        var response = await _http.PostAsJsonAsync($"api/games/{gameId}/move", body, ct);
        return await ReadResponseAsync<MoveResponse>(response, "MovePiece");
    }

    // ── Game result ───────────────────────────────────────────────────────────

    /// <summary>
    /// Retrieves the final result of a finished game.
    /// <c>GET /api/games/{gameId}/result</c>
    /// Returns null if the game is not yet finished (409) — retry later.
    /// </summary>
    public async Task<GameResult?> GetResultAsync(Guid gameId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync(BuildUri($"api/games/{gameId}/result"), ct);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            // Game not finished yet
            return null;
        }

        return await ReadResponseAsync<GameResult>(response, "GetResult");
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private Uri BuildUri(string relativePath) => new(_http.BaseAddress!, relativePath);

    private static async Task<T?> ReadResponseAsync<T>(HttpResponseMessage response, string operationName) where T : class
    {
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        }

        await PrintApiErrorAsync(response, operationName);
        return null;
    }

    private static async Task PrintApiErrorAsync(HttpResponseMessage response, string operationName)
    {
        var body = await response.Content.ReadAsStringAsync();
        var status = (int)response.StatusCode;
        var label = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => "Bad Request — check your request body",
            HttpStatusCode.Forbidden  => "Forbidden — check your X-Bot-Token",
            HttpStatusCode.NotFound   => "Not Found — check the game/queue ID",
            HttpStatusCode.Conflict   => "Conflict — already queued or acted this turn",
            _                         => response.ReasonPhrase ?? "Unknown error"
        };
        Console.WriteLine($"[API ERROR] {operationName} → {status} {label}");
        Console.WriteLine($"            Response: {body}");
    }

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();
}
