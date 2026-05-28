using MediatR;
using ScrambleCoin.Application.Tournament.AddParticipant;
using ScrambleCoin.Application.Tournament.CancelTournament;
using ScrambleCoin.Application.Tournament.CreateTournament;
using ScrambleCoin.Application.Tournament.GetBotGames;
using ScrambleCoin.Application.Tournament.GetBracket;
using ScrambleCoin.Application.Tournament.GetStandings;
using ScrambleCoin.Application.Tournament.StartTournament;
using ScrambleCoin.Domain.Exceptions;

namespace ScrambleCoin.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for tournament management.
/// </summary>
public static class TournamentEndpoints
{

    public static void MapTournamentEndpoints(this WebApplication app)
    {
        // POST /api/tournament — organiser creates a tournament
        app.MapPost("/api/tournament", CreateTournament)
            .WithName("CreateTournament")
            .WithSummary("Create a tournament")
            .WithDescription(
                "Organiser creates a named tournament. Requires X-Admin-Key header. " +
                "Bots can be registered via POST /api/tournament/{id}/participants.")
            .WithTags("Tournament");

        // POST /api/tournament/{id}/participants — register a bot
        app.MapPost("/api/tournament/{id:guid}/participants", AddParticipant)
            .WithName("AddTournamentParticipant")
            .WithSummary("Register a bot for the tournament")
            .WithDescription("Registers a bot participant before the tournament starts. Requires X-Admin-Key header.")
            .WithTags("Tournament");

        // POST /api/tournament/{id}/start — lock participants and generate schedule
        app.MapPost("/api/tournament/{id:guid}/start", StartTournament)
            .WithName("StartTournament")
            .WithSummary("Start the tournament")
            .WithDescription(
                "Locks participants and generates the round-robin group stage schedule. " +
                "Creates all group games automatically. Requires X-Admin-Key header.")
            .WithTags("Tournament");

        // POST /api/tournament/{id}/cancel — cancel the tournament
        app.MapPost("/api/tournament/{id:guid}/cancel", CancelTournament)
            .WithName("CancelTournament")
            .WithSummary("Cancel the tournament")
            .WithDescription("Cancels an active tournament. Requires X-Admin-Key header.")
            .WithTags("Tournament");

        // GET /api/tournament/{id}/standings — group stage table
        app.MapGet("/api/tournament/{id:guid}/standings", GetStandings)
            .WithName("GetTournamentStandings")
            .WithSummary("Get group stage standings")
            .WithDescription(
                "Returns the current group stage standings. " +
                "Lazily syncs completed game results into the standings. " +
                "Does not advance the tournament to the knockout stage.")
            .WithTags("Tournament");

        // GET /api/tournament/{id}/bracket — full bracket
        app.MapGet("/api/tournament/{id:guid}/bracket", GetBracket)
            .WithName("GetTournamentBracket")
            .WithSummary("Get tournament bracket")
            .WithDescription(
                "Returns the full bracket including group matches and knockout rounds. " +
                "Lazily syncs game results and creates next-round games when a round completes (idempotent). " +
                "Bot tokens are not included here; use GET /api/tournament/{id}/bots/{botId}/games instead.")
            .WithTags("Tournament");

        // GET /api/tournament/{id}/bots/{botId}/games — per-bot token discovery
        app.MapGet("/api/tournament/{id:guid}/bots/{botId:guid}/games", GetBotGames)
            .WithName("GetBotTournamentGames")
            .WithSummary("Get a bot's tournament games and tokens")
            .WithDescription(
                "Returns game IDs and authentication tokens for a specific bot across all stages. " +
                "Only the requesting bot's own tokens are returned. " +
                "Bots should call this after the tournament starts to discover their current game.")
            .WithTags("Tournament");
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    /// <summary>Admin creates a tournament. Requires <c>X-Admin-Key: scramblecoin-admin</c>.</summary>
    private static async Task<IResult> CreateTournament(
        CreateTournamentRequest body,
        HttpRequest httpRequest,
        ISender sender,
        CancellationToken ct)
    {
        if (!AdminAuth.IsValid(httpRequest))
            return AdminAuth.Unauthorized();

        try
        {
            var result = await sender.Send(
                new CreateTournamentCommand(
                    body.Name,
                    body.MaxParticipants,
                    body.TopN ?? 4),
                ct);

            return Results.Created($"/api/tournament/{result.TournamentId}", new { tournamentId = result.TournamentId });
        }
        catch (DomainException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }
    }

    /// <summary>Admin registers a bot for the tournament.</summary>
    private static async Task<IResult> AddParticipant(
        Guid id,
        AddParticipantRequest body,
        HttpRequest httpRequest,
        ISender sender,
        CancellationToken ct)
    {
        if (!AdminAuth.IsValid(httpRequest))
            return AdminAuth.Unauthorized();

        try
        {
            await sender.Send(
                new AddTournamentParticipantCommand(id, body.BotId, body.BotName, body.Lineup),
                ct);

            return Results.NoContent();
        }
        catch (TournamentNotFoundException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found");
        }
        catch (TournamentInvalidStateException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict");
        }
        catch (DomainException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }
    }

    /// <summary>Admin starts the tournament: locks participants and creates all group games.</summary>
    private static async Task<IResult> StartTournament(
        Guid id,
        HttpRequest httpRequest,
        ISender sender,
        CancellationToken ct)
    {
        if (!AdminAuth.IsValid(httpRequest))
            return AdminAuth.Unauthorized();

        try
        {
            await sender.Send(new StartTournamentCommand(id), ct);
            return Results.Ok(new { message = "Tournament started. Group stage games have been created." });
        }
        catch (TournamentNotFoundException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found");
        }
        catch (TournamentInvalidStateException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict");
        }
        catch (DomainException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }
    }

    /// <summary>Admin cancels the tournament.</summary>
    private static async Task<IResult> CancelTournament(
        Guid id,
        HttpRequest httpRequest,
        ISender sender,
        CancellationToken ct)
    {
        if (!AdminAuth.IsValid(httpRequest))
            return AdminAuth.Unauthorized();

        try
        {
            await sender.Send(new CancelTournamentCommand(id), ct);
            return Results.Ok(new { message = "Tournament cancelled." });
        }
        catch (TournamentNotFoundException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found");
        }
        catch (TournamentInvalidStateException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict");
        }
    }

    /// <summary>Returns the current group stage standings.</summary>
    private static async Task<IResult> GetStandings(
        Guid id,
        ISender sender,
        CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(new GetTournamentStandingsQuery(id), ct);
            return Results.Ok(result);
        }
        catch (TournamentNotFoundException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found");
        }
    }

    /// <summary>Returns the full tournament bracket with game IDs (without bot tokens).</summary>
    private static async Task<IResult> GetBracket(
        Guid id,
        ISender sender,
        CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(new GetTournamentBracketQuery(id), ct);
            return Results.Ok(result);
        }
        catch (TournamentNotFoundException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found");
        }
    }

    // ── Request bodies ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns game IDs and tokens for a specific bot across all tournament stages.
    /// Only that bot's own tokens are returned.
    /// </summary>
    private static async Task<IResult> GetBotGames(
        Guid id,
        Guid botId,
        ISender sender,
        CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(new GetBotGamesQuery(id, botId), ct);
            return Results.Ok(result);
        }
        catch (TournamentNotFoundException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found");
        }
    }

    // ── Request bodies ────────────────────────────────────────────────────────

    /// <summary>Request body for <c>POST /api/tournament</c>.</summary>
    /// <param name="Name">Tournament display name.</param>
    /// <param name="MaxParticipants">Maximum number of bots (minimum 2).</param>
    /// <param name="TopN">Number of group-stage qualifiers for the knockout stage (default: 4).</param>
    private sealed record CreateTournamentRequest(string Name, int MaxParticipants, int? TopN);

    /// <summary>Request body for <c>POST /api/tournament/{id}/participants</c>.</summary>
    /// <param name="BotId">Stable identifier for the bot (used as their game token throughout the tournament).</param>
    /// <param name="BotName">Human-readable display name for this bot.</param>
    /// <param name="Lineup">Ordered list of piece names the bot will use in all their tournament games.</param>
    private sealed record AddParticipantRequest(
        Guid BotId,
        string BotName,
        IReadOnlyList<string> Lineup);
}
