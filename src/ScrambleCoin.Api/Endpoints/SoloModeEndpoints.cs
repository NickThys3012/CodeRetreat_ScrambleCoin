using MediatR;
using ScrambleCoin.Application.Games.SoloMode.CreateSoloGame;
using ScrambleCoin.Application.Games.SoloMode.GetStarterPieces;
using ScrambleCoin.Application.Games.SoloMode.GetUnlockedPieces;
using ScrambleCoin.Application.Games.SoloMode.GetVillainPath;
using ScrambleCoin.Domain.Exceptions;

namespace ScrambleCoin.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for solo mode: villain path, unlocked pieces, and solo game creation.
/// </summary>
internal static class SoloModeEndpoints
{
    public static void MapSoloModeEndpoints(this WebApplication app)
    {
        // GET /api/solo/path?botId={botId} — Get a villain tree with lock status for a bot
        app.MapGet("/api/solo/path", GetVillainPath)
            .WithName("GetVillainPath")
            .WithTags("Solo")
            .WithDescription("Get the villain unlock tree for a bot, showing available, locked, and defeated villains.");

        // GET /api/solo/pieces?botId={botId} — Get all pieces available to a bot
        app.MapGet("/api/solo/pieces", GetUnlockedPieces)
            .WithName("GetUnlockedPieces")
            .WithTags("Solo")
            .WithDescription("Get all pieces available to a bot (starter pieces + defeated villain rewards).");

        // GET /api/solo/starter-pieces — Get default starter pieces
        app.MapGet("/api/solo/starter-pieces", GetStarterPieces)
            .WithName("GetStarterPieces")
            .WithTags("Solo")
            .WithDescription("Get the default starter pieces available to all bots.");

        // POST /api/games/solo — Create a solo game
        app.MapPost("/api/games/solo", CreateSoloGame)
            .WithName("CreateSoloGame")
            .WithTags("Solo")
            .WithDescription("Create a solo game where a bot challenges a specific villain.");
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private static async Task<IResult> GetVillainPath(
        Guid botId,
        ISender sender,
        CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(new GetVillainPathQuery(botId), ct);
            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error");
        }
    }

    private static async Task<IResult> GetUnlockedPieces(
        Guid botId,
        ISender sender,
        CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(new GetUnlockedPiecesQuery(botId), ct);
            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error");
        }
    }

    private static async Task<IResult> GetStarterPieces(
        ISender sender,
        CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(new GetStarterPiecesQuery(), ct);
            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error");
        }
    }

    private static async Task<IResult> CreateSoloGame(
        CreateSoloGameRequest body,
        ISender sender,
        CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(
                new CreateSoloGameCommand(body.BotId, body.VillainId),
                ct);

            return Results.Created($"/api/games/{result.GameId}", result);
        }
        catch (DomainException ex) when (ex.Message.Contains("locked", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict,
                title: "Villain Locked");
        }
        catch (DomainException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error");
        }
    }

    // ── Request bodies ────────────────────────────────────────────────────────

    private sealed record CreateSoloGameRequest(
        Guid BotId,
        string VillainId);
}
