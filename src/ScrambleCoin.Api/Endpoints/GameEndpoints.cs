using MediatR;
using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Application.Games.CreateGame;
using ScrambleCoin.Application.Games.GetBoardState;
using ScrambleCoin.Application.Games.JoinGame;
using ScrambleCoin.Application.Games.PlacePiece;
using ScrambleCoin.Application.Games.ReplacePiece;
using ScrambleCoin.Application.Games.SkipPlacement;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Services;
using ScrambleCoin.Domain.Exceptions;
using ScrambleCoin.Domain.ValueObjects;

namespace ScrambleCoin.Api.Endpoints;

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

        // GET /api/games/{gameId}/state — bot reads current board state
        app.MapGet("/api/games/{gameId}/state", GetBoardState)
            .WithName("GetBoardState")
            .WithTags("Games");

        // POST /api/games/{gameId}/place — bot submits placement decision
        app.MapPost("/api/games/{gameId}/place", PlacePiece)
            .WithName("PlacePiece")
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
            return Results.Created($"/api/games/{gameId}", new { playerId = result.PlayerId, token = result.Token });
        }
        catch (GameFullException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict,
                title: "Game Full");
        }
        catch (GameNotFoundException ex)
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

    /// <summary>Bot submits a placement decision (place, replace, or skip) during PlacePhase.</summary>
    private static async Task<IResult> PlacePiece(
        Guid gameId,
        PlacementRequest body,
        HttpRequest httpRequest,
        ISender sender,
        IBotRegistrationRepository botRegistrationRepository,
        IGameRepository gameRepository,
        CancellationToken ct)
    {
        // 1. Validate X-Bot-Token header
        if (!httpRequest.Headers.TryGetValue("X-Bot-Token", out var tokenHeader) ||
            !Guid.TryParse(tokenHeader, out var botToken))
        {
            return Results.Problem(
                detail: "Missing or invalid X-Bot-Token header.",
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden");
        }

        // 2. Resolve player identity from token
        var registration = await botRegistrationRepository.GetByTokenAsync(botToken, ct);
        if (registration is null || registration.GameId != gameId)
        {
            return Results.Problem(
                detail: "The provided token does not match any player in this game.",
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden");
        }

        var playerId = registration.PlayerId;

        try
        {
            switch (body.Action?.ToLowerInvariant())
            {
                case "place":
                    if (body.PieceId is null || body.Position is null)
                        return Results.Problem(
                            detail: "Action 'place' requires 'pieceId' and 'position'.",
                            statusCode: StatusCodes.Status400BadRequest,
                            title: "Bad Request");
                    await sender.Send(
                        new PlacePieceCommand(
                            gameId,
                            playerId,
                            body.PieceId.Value,
                            new Position(body.Position.Row, body.Position.Col)),
                        ct);
                    break;

                case "replace":
                    if (body.PieceId is null || body.ReplacedPieceId is null || body.Position is null)
                        return Results.Problem(
                            detail: "Action 'replace' requires 'pieceId', 'replacedPieceId', and 'position'.",
                            statusCode: StatusCodes.Status400BadRequest,
                            title: "Bad Request");
                    await sender.Send(
                        new ReplacePieceCommand(
                            gameId,
                            playerId,
                            ExistingPieceId: body.ReplacedPieceId.Value,
                            NewPieceId: body.PieceId.Value,
                            TargetPosition: new Position(body.Position.Row, body.Position.Col)),
                        ct);
                    break;

                case "skip":
                    await sender.Send(new SkipPlacementCommand(gameId, playerId), ct);
                    break;

                default:
                    return Results.Problem(
                        detail: $"Unknown action '{body.Action}'. Valid values are: place, replace, skip.",
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Bad Request");
            }

            // 3. Return current phase state
            var game = await gameRepository.GetByIdAsync(gameId, ct);
            return Results.Ok(new
            {
                phase = game.CurrentPhase?.ToString(),
                activePlayer = game.MovePhaseActivePlayer?.ToString()
            });
        }
        catch (PlayerAlreadyActedException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict");
        }
        catch (UnauthorizedGameAccessException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden");
        }
        catch (GameNotFoundException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found");
        }
        catch (DomainException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }
    }

    // ── Request bodies ────────────────────────────────────────────────────────

    private sealed record JoinGameRequest(IReadOnlyList<string> Lineup);

    private sealed record QueueRequest(IReadOnlyList<string> Lineup);

    /// <summary>
    /// Request body for <c>POST /api/games/{gameId}/place</c>.
    /// </summary>
    /// <param name="Action">One of: "place", "replace", "skip".</param>
    /// <param name="PieceId">The piece to place or use as replacement (required for "place" and "replace").</param>
    /// <param name="ReplacedPieceId">The on-board piece to remove (required for "replace" only).</param>
    /// <param name="Position">Target board position (required for "place" and "replace").</param>
    private sealed record PlacementRequest(
        string? Action,
        Guid? PieceId,
        Guid? ReplacedPieceId,
        PositionRequest? Position);

    private sealed record PositionRequest(int Row, int Col);

    /// <summary>Bot reads the current board state for a game.</summary>
    private static async Task<IResult> GetBoardState(
        Guid gameId,
        HttpRequest httpRequest,
        ISender sender,
        CancellationToken ct)
    {
        if (!httpRequest.Headers.TryGetValue("X-Bot-Token", out var tokenHeader) ||
            !Guid.TryParse(tokenHeader, out var botToken))
        {
            return Results.Problem(
                detail: "Missing or invalid X-Bot-Token header.",
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden");
        }

        try
        {
            var result = await sender.Send(new GetBoardStateQuery(gameId, botToken), ct);
            return Results.Ok(result);
        }
        catch (UnauthorizedGameAccessException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden");
        }
        catch (GameNotFoundException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found");
        }
    }
}

