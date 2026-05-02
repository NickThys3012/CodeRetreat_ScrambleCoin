using MediatR;
using ScrambleCoin.Application.Games.CreateGame;
using ScrambleCoin.Application.Games.JoinGame;
using ScrambleCoin.Application.Services;
using ScrambleCoin.Domain.Exceptions;

namespace ScrambleCoin.Web.Endpoints;

/// <summary>
/// Minimal API endpoints for game session management and bot registration.
/// </summary>
public static class GameEndpoints
{
    private const string AdminKey = "scramblecoin-admin";

    public static void MapGameEndpoints(this WebApplication app)
    {
        // POST /api/games — admin creates a game shell
        app.MapPost("/api/games", CreateGame)
            .WithName("CreateGame")
            .WithTags("Games");

        // POST /api/games/{gameId}/join — bot registers and submits lineup
        app.MapPost("/api/games/{gameId}/join", JoinGame)
            .WithName("JoinGame")
            .WithTags("Games");

        // POST /api/games/queue — bot joins matchmaking queue
        app.MapPost("/api/games/queue", QueueBot)
            .WithName("QueueBot")
            .WithTags("Games");

        // GET /api/games/queue/{queueId} — poll matchmaking status
        app.MapGet("/api/games/queue/{queueId}", PollQueue)
            .WithName("PollQueue")
            .WithTags("Games");
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    /// <summary>Admin creates a game shell. Requires <c>X-Admin-Key: scramblecoin-admin</c>.</summary>
    private static async Task<IResult> CreateGame(
        HttpRequest httpRequest,
        ISender sender,
        CancellationToken ct)
    {
        if (!httpRequest.Headers.TryGetValue("X-Admin-Key", out var adminKey) ||
            adminKey != AdminKey)
        {
            return Results.Problem(
                detail: "Missing or invalid X-Admin-Key header.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized");
        }

        var result = await sender.Send(new CreateGameCommand(), ct);

        return Results.Created($"/api/games/{result.GameId}", new { gameId = result.GameId });
    }

    /// <summary>Bot joins a game and submits a lineup of 5 piece names.</summary>
    private static async Task<IResult> JoinGame(
        Guid gameId,
        JoinGameRequest body,
        ISender sender,
        CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(new JoinGameCommand(gameId, body.Lineup), ct);
            return Results.Ok(new { playerId = result.PlayerId, token = result.Token });
        }
        catch (GameFullException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict,
                title: "Game Full");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("was not found"))
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found");
        }
    }

    /// <summary>Bot joins the matchmaking queue with a lineup.</summary>
    private static async Task<IResult> QueueBot(
        QueueRequest body,
        IQueueService queueService,
        CancellationToken ct)
    {
        var entry = await queueService.EnqueueAsync(body.Lineup, ct);

        if (entry.Status == "matched")
        {
            return Results.Ok(new
            {
                gameId = entry.GameId,
                playerId = entry.PlayerId,
                token = entry.Token
            });
        }

        // 202 Accepted — bot is waiting in the queue
        return Results.Accepted(
            $"/api/games/queue/{entry.QueueId}",
            new { queueId = entry.QueueId });
    }

    /// <summary>Polls a queue entry for matchmaking status.</summary>
    private static async Task<IResult> PollQueue(
        Guid queueId,
        IQueueService queueService,
        CancellationToken ct)
    {
        var entry = await queueService.PollAsync(queueId, ct);

        if (entry is null)
        {
            return Results.Problem(
                detail: $"Queue entry '{queueId}' was not found.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found");
        }

        if (entry.Status == "waiting")
        {
            return Results.Ok(new { status = "waiting" });
        }

        return Results.Ok(new
        {
            status = "matched",
            gameId = entry.GameId,
            playerId = entry.PlayerId,
            token = entry.Token
        });
    }

    // ── Request bodies ────────────────────────────────────────────────────────

    private sealed record JoinGameRequest(IReadOnlyList<string> Lineup);

    private sealed record QueueRequest(IReadOnlyList<string> Lineup);
}
