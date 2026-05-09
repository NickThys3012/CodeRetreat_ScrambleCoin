using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Domain.BotRegistrations;
using ScrambleCoin.Domain.Entities;
using ScrambleCoin.Domain.Enums;
using ScrambleCoin.Domain.Obstacles;
using ScrambleCoin.Domain.ValueObjects;
using ScrambleCoin.Infrastructure.Persistence;

namespace ScrambleCoin.Web.Tests;

/// <summary>
/// Integration tests for <c>POST /api/games/{gameId}/place</c> (Issue #39).
/// Uses <see cref="WebApplicationFactory{TEntryPoint}"/> with an in-memory database.
/// </summary>
public class PlacementEndpointTests : IClassFixture<PlacementEndpointTests.TestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly TestWebApplicationFactory _factory;

    public PlacementEndpointTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── Test factory ──────────────────────────────────────────────────────────

    public sealed class TestWebApplicationFactory : WebApplicationFactory<ScrambleCoin.Api.ApiMarker>
    {
        // Unique DB name per factory instance so test classes don't share state.
        private readonly string _dbName = $"PlacementTestDb_{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                // Remove every DbContext-related registration added by Program.cs.
                var descriptorsToRemove = services
                    .Where(d =>
                        d.ServiceType == typeof(DbContextOptions<ScrambleCoinDbContext>) ||
                        d.ServiceType == typeof(DbContextOptions) ||
                        d.ServiceType == typeof(ScrambleCoinDbContext))
                    .ToList();

                foreach (var d in descriptorsToRemove)
                    services.Remove(d);

                // Re-register using a factory that builds fresh DbContextOptions with
                // ONLY the InMemory provider.
                var dbName = _dbName;
                services.AddScoped<ScrambleCoinDbContext>(_ =>
                {
                    var opts = new DbContextOptionsBuilder<ScrambleCoinDbContext>()
                        .UseInMemoryDatabase(dbName)
                        .Options;
                    return new ScrambleCoinDbContext(opts);
                });

                services.Configure<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckServiceOptions>(
                    opts => opts.Registrations.Clear());
            });

            builder.UseUrls("http://127.0.0.1:0");
        }
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a game in <see cref="TurnPhase.PlacePhase"/> (Turn 1) with two bot registrations.
    /// Returns (game, tokenP1, tokenP2, p1Pieces, p2Pieces).
    /// </summary>
    private async Task<(Game game, Guid tokenP1, Guid tokenP2, List<Piece> p1Pieces, List<Piece> p2Pieces)>
        SeedGameInPlacePhaseAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var gameRepo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var botRepo = scope.ServiceProvider.GetRequiredService<IBotRegistrationRepository>();

        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var p1Pieces = Enumerable.Range(0, 5)
            .Select(i => new Piece(Guid.NewGuid(), $"P1Piece{i}", p1, EntryPointType.Borders, MovementType.Orthogonal, 3, 1))
            .ToList();
        var p2Pieces = Enumerable.Range(0, 5)
            .Select(i => new Piece(Guid.NewGuid(), $"P2Piece{i}", p2, EntryPointType.Borders, MovementType.Orthogonal, 3, 1))
            .ToList();

        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(p1Pieces));
        game.SetLineup(p2, new Lineup(p2Pieces));
        game.Start();         // → CoinSpawn Turn 1
        game.AdvancePhase();  // → PlacePhase Turn 1

        await gameRepo.SaveAsync(game);

        var tokenP1 = Guid.NewGuid();
        var tokenP2 = Guid.NewGuid();
        await botRepo.SaveAsync(new BotRegistration(tokenP1, p1, game.Id));
        await botRepo.SaveAsync(new BotRegistration(tokenP2, p2, game.Id));

        return (game, tokenP1, tokenP2, p1Pieces, p2Pieces);
    }

    /// <summary>
    /// Seeds a game in <see cref="TurnPhase.MovePhase"/> (both players already skipped placement).
    /// </summary>
    private async Task<(Game game, Guid tokenP1, Guid tokenP2)> SeedGameInMovePhaseAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var gameRepo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var botRepo = scope.ServiceProvider.GetRequiredService<IBotRegistrationRepository>();

        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var p1Pieces = Enumerable.Range(0, 5)
            .Select(i => new Piece(Guid.NewGuid(), $"P1Piece{i}", p1, EntryPointType.Borders, MovementType.Orthogonal, 3, 1))
            .ToList();
        var p2Pieces = Enumerable.Range(0, 5)
            .Select(i => new Piece(Guid.NewGuid(), $"P2Piece{i}", p2, EntryPointType.Borders, MovementType.Orthogonal, 3, 1))
            .ToList();

        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(p1Pieces));
        game.SetLineup(p2, new Lineup(p2Pieces));
        game.Start();            // → CoinSpawn Turn 1
        game.AdvancePhase();     // → PlacePhase Turn 1
        game.SkipPlacement(p1); // P1 skips (1/2)
        game.SkipPlacement(p2); // P2 skips (2/2) → auto-advances to MovePhase

        await gameRepo.SaveAsync(game);

        var tokenP1 = Guid.NewGuid();
        var tokenP2 = Guid.NewGuid();
        await botRepo.SaveAsync(new BotRegistration(tokenP1, p1, game.Id));
        await botRepo.SaveAsync(new BotRegistration(tokenP2, p2, game.Id));

        return (game, tokenP1, tokenP2);
    }

    /// <summary>
    /// Seeds a game in <see cref="TurnPhase.PlacePhase"/> with a Rock obstacle on edge tile (0, 2).
    /// The edge position is a valid Borders entry point but is covered by the obstacle.
    /// </summary>
    private async Task<(Game game, Guid tokenP1, Guid tokenP2, Guid pieceId)>
        SeedGameWithObstacleAtEdgeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var gameRepo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var botRepo = scope.ServiceProvider.GetRequiredService<IBotRegistrationRepository>();

        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var p1Pieces = Enumerable.Range(0, 5)
            .Select(i => new Piece(Guid.NewGuid(), $"P1Piece{i}", p1, EntryPointType.Borders, MovementType.Orthogonal, 3, 1))
            .ToList();
        var p2Pieces = Enumerable.Range(0, 5)
            .Select(i => new Piece(Guid.NewGuid(), $"P2Piece{i}", p2, EntryPointType.Borders, MovementType.Orthogonal, 3, 1))
            .ToList();

        var board = new Board();
        // Place a Rock on edge tile (0, 2) — valid Borders entry point, but covered by obstacle.
        board.AddRock(new Rock(new Position(0, 2)));

        var game = new Game(p1, p2, board);
        game.SetLineup(p1, new Lineup(p1Pieces));
        game.SetLineup(p2, new Lineup(p2Pieces));
        game.Start();        // → CoinSpawn Turn 1
        game.AdvancePhase(); // → PlacePhase Turn 1

        await gameRepo.SaveAsync(game);

        var tokenP1 = Guid.NewGuid();
        var tokenP2 = Guid.NewGuid();
        await botRepo.SaveAsync(new BotRegistration(tokenP1, p1, game.Id));
        await botRepo.SaveAsync(new BotRegistration(tokenP2, p2, game.Id));

        return (game, tokenP1, tokenP2, p1Pieces[0].Id);
    }

    /// <summary>
    /// Seeds a game in <see cref="TurnPhase.PlacePhase"/> on Turn 2 where P1 already has
    /// a piece on the board from Turn 1. Provides IDs for the on-board and off-board pieces.
    /// </summary>
    private async Task<(Game game, Guid tokenP1, Guid tokenP2, Guid onBoardPieceId, Guid offBoardPieceId)>
        SeedGameInPlacePhaseWithOneBoardPieceAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var gameRepo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var botRepo = scope.ServiceProvider.GetRequiredService<IBotRegistrationRepository>();

        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var p1Pieces = Enumerable.Range(0, 5)
            .Select(i => new Piece(Guid.NewGuid(), $"P1Piece{i}", p1, EntryPointType.Borders, MovementType.Orthogonal, 3, 1))
            .ToList();
        var p2Pieces = Enumerable.Range(0, 5)
            .Select(i => new Piece(Guid.NewGuid(), $"P2Piece{i}", p2, EntryPointType.Borders, MovementType.Orthogonal, 3, 1))
            .ToList();

        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(p1Pieces));
        game.SetLineup(p2, new Lineup(p2Pieces));
        game.Start();           // → CoinSpawn Turn 1
        game.AdvancePhase();    // → PlacePhase Turn 1

        // P1 places piece0 at corner (0, 0); P2 skips → auto-advance to MovePhase
        game.PlacePiece(p1, p1Pieces[0].Id, new Position(0, 0));
        game.SkipPlacement(p2); // → MovePhase Turn 1

        game.AdvancePhase();    // → CoinSpawn Turn 2
        game.AdvancePhase();    // → PlacePhase Turn 2

        await gameRepo.SaveAsync(game);

        var tokenP1 = Guid.NewGuid();
        var tokenP2 = Guid.NewGuid();
        await botRepo.SaveAsync(new BotRegistration(tokenP1, p1, game.Id));
        await botRepo.SaveAsync(new BotRegistration(tokenP2, p2, game.Id));

        // p1Pieces[0] is on the board; p1Pieces[1] is off the board
        return (game, tokenP1, tokenP2, p1Pieces[0].Id, p1Pieces[1].Id);
    }

    // ── AC 1 : action "place" → 200 with { phase, activePlayer } ─────────────

    [Fact]
    public async Task Place_WithPlaceAction_Returns200()
    {
        // Arrange
        var (game, tokenP1, _, p1Pieces, _) = await SeedGameInPlacePhaseAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        var body = new
        {
            action = "place",
            pieceId = p1Pieces[0].Id,
            position = new { row = 0, col = 0 }
        };

        // Act
        var response = await client.PostAsJsonAsync($"/api/games/{game.Id}/place", body);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Place_WithPlaceAction_ResponseContainsPhaseField()
    {
        // Arrange
        var (game, tokenP1, _, p1Pieces, _) = await SeedGameInPlacePhaseAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        var body = new
        {
            action = "place",
            pieceId = p1Pieces[0].Id,
            position = new { row = 0, col = 1 }
        };

        // Act
        var response = await client.PostAsJsonAsync($"/api/games/{game.Id}/place", body);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.True(json.RootElement.TryGetProperty("phase", out _),
            "Response JSON must contain a 'phase' field.");
    }

    [Fact]
    public async Task Place_WithPlaceAction_ResponseContainsActivePlayerField()
    {
        // Arrange
        var (game, tokenP1, _, p1Pieces, _) = await SeedGameInPlacePhaseAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        var body = new
        {
            action = "place",
            pieceId = p1Pieces[0].Id,
            position = new { row = 0, col = 2 }
        };

        // Act
        var response = await client.PostAsJsonAsync($"/api/games/{game.Id}/place", body);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.True(json.RootElement.TryGetProperty("activePlayer", out _),
            "Response JSON must contain an 'activePlayer' field (may be null).");
    }

    // ── AC 2 : action "replace" → 200 ────────────────────────────────────────

    [Fact]
    public async Task Place_WithReplaceAction_Returns200()
    {
        // Arrange: game in PlacePhase Turn 2 with P1's piece[0] already on board at (0, 0)
        var (game, tokenP1, _, onBoardPieceId, offBoardPieceId) =
            await SeedGameInPlacePhaseWithOneBoardPieceAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        var body = new
        {
            action = "replace",
            pieceId = offBoardPieceId,          // new piece (currently off-board)
            replacedPieceId = onBoardPieceId,   // piece to remove (currently at (0, 0))
            // position is no longer required for replace — new piece lands at old piece's tile
        };

        // Act
        var response = await client.PostAsJsonAsync($"/api/games/{game.Id}/place", body);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── AC 3 : action "skip" → 200 ────────────────────────────────────────────

    [Fact]
    public async Task Place_WithSkipAction_Returns200()
    {
        // Arrange
        var (game, tokenP1, _, _, _) = await SeedGameInPlacePhaseAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        var body = new { action = "skip" };

        // Act
        var response = await client.PostAsJsonAsync($"/api/games/{game.Id}/place", body);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── AC 4 : invalid action → 400 ───────────────────────────────────────────

    [Fact]
    public async Task Place_WithUnknownAction_Returns400()
    {
        // Arrange
        var (game, tokenP1, _, _, _) = await SeedGameInPlacePhaseAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        var body = new { action = "fly" };

        // Act
        var response = await client.PostAsJsonAsync($"/api/games/{game.Id}/place", body);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Place_WithNullAction_Returns400()
    {
        // Arrange
        var (game, tokenP1, _, _, _) = await SeedGameInPlacePhaseAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // action is omitted (null) — should hit the default case
        var body = new { pieceId = (Guid?)null, position = (object?)null };

        // Act
        var response = await client.PostAsJsonAsync($"/api/games/{game.Id}/place", body);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── AC 5 : wrong phase (MovePhase) → 400 ──────────────────────────────────

    [Fact]
    public async Task Place_DuringMovePhase_Returns400()
    {
        // Arrange: game is already in MovePhase
        var (game, tokenP1, _) = await SeedGameInMovePhaseAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        var body = new { action = "skip" };

        // Act
        var response = await client.PostAsJsonAsync($"/api/games/{game.Id}/place", body);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── AC 6 : invalid position (obstacle tile) → 400 ────────────────────────

    [Fact]
    public async Task Place_OnObstacleTile_Returns400()
    {
        // Arrange: board has a Rock at edge tile (0, 2) — valid entry point but covered
        var (game, tokenP1, _, pieceId) = await SeedGameWithObstacleAtEdgeAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        var body = new
        {
            action = "place",
            pieceId,
            position = new { row = 0, col = 2 } // Rock is here
        };

        // Act
        var response = await client.PostAsJsonAsync($"/api/games/{game.Id}/place", body);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── AC 7 : missing X-Bot-Token → 403 ─────────────────────────────────────

    [Fact]
    public async Task Place_WithoutToken_Returns403()
    {
        // Arrange
        var (game, _, _, _, _) = await SeedGameInPlacePhaseAsync();
        var client = _factory.CreateClient();
        // No X-Bot-Token header

        var body = new { action = "skip" };

        // Act
        var response = await client.PostAsJsonAsync($"/api/games/{game.Id}/place", body);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── AC 8 : X-Bot-Token doesn't match any player → 403 ────────────────────

    [Fact]
    public async Task Place_WithUnknownToken_Returns403()
    {
        // Arrange
        var (game, _, _, _, _) = await SeedGameInPlacePhaseAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", Guid.NewGuid().ToString()); // random token

        var body = new { action = "skip" };

        // Act
        var response = await client.PostAsJsonAsync($"/api/games/{game.Id}/place", body);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── AC 9 : gameId doesn't exist → 404 ────────────────────────────────────

    [Fact]
    public async Task Place_WithNonExistentGameId_Returns404()
    {
        // Arrange: use a valid token from a real game, but target a different (non-existent) gameId
        var (_, tokenP1, _, _, _) = await SeedGameInPlacePhaseAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Use the tokenP1 but point to a non-existent game
        // The token lookup checks registration.GameId != gameId, so this returns 403 first.
        // To get a 404 we need a token registered to the nonExistentGameId — which doesn't exist.
        // Instead, we call without a registered token so the registration lookup returns null → 403.
        // For a true 404, we need to reach the game repository. Let's test by using a valid token
        // for another game against a fresh random gameId that was never created.
        // NOTE: The endpoint checks token→gameId match BEFORE loading the game, so a mismatched
        // gameId returns 403. To trigger 404 we need a token whose GameId points to a deleted game.
        // We simulate by registering a token pointing to a non-existent gameId.
        using var scope = _factory.Services.CreateScope();
        var botRepo = scope.ServiceProvider.GetRequiredService<IBotRegistrationRepository>();
        var ghostGameId = Guid.NewGuid();
        var ghostToken = Guid.NewGuid();
        await botRepo.SaveAsync(new BotRegistration(ghostToken, Guid.NewGuid(), ghostGameId));

        var client2 = _factory.CreateClient();
        client2.DefaultRequestHeaders.Add("X-Bot-Token", ghostToken.ToString());
        var body = new { action = "skip" };

        // Act
        var response = await client2.PostAsJsonAsync($"/api/games/{ghostGameId}/place", body);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── AC 10 : same player acts twice in same phase → 409 ───────────────────

    [Fact]
    public async Task Place_SamePlayerActsTwice_Returns409()
    {
        // Arrange
        var (game, tokenP1, _, p1Pieces, _) = await SeedGameInPlacePhaseAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // First action: P1 skips (succeeds)
        await client.PostAsJsonAsync($"/api/games/{game.Id}/place", new { action = "skip" });

        // Second action: P1 tries to skip again (conflict)
        var body = new { action = "skip" };

        // Act
        var response = await client.PostAsJsonAsync($"/api/games/{game.Id}/place", body);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── AC 11 : phase auto-advances to MovePhase when both players act ────────

    [Fact]
    public async Task Place_BothPlayersSkip_PhaseAdvancesToMovePhase()
    {
        // Arrange
        var (game, tokenP1, tokenP2, _, _) = await SeedGameInPlacePhaseAsync();

        var clientP1 = _factory.CreateClient();
        clientP1.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        var clientP2 = _factory.CreateClient();
        clientP2.DefaultRequestHeaders.Add("X-Bot-Token", tokenP2.ToString());

        // P1 skips
        await clientP1.PostAsJsonAsync($"/api/games/{game.Id}/place", new { action = "skip" });

        // Act: P2 skips — the response should reflect the auto-advanced phase
        var response = await clientP2.PostAsJsonAsync($"/api/games/{game.Id}/place", new { action = "skip" });
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert: phase advanced to MovePhase
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(json.RootElement.TryGetProperty("phase", out var phaseElement));
        Assert.Equal("MovePhase", phaseElement.GetString());
    }

    [Fact]
    public async Task Place_BothPlayersPlace_PhaseAdvancesToMovePhase()
    {
        // Arrange
        var (game, tokenP1, tokenP2, p1Pieces, p2Pieces) = await SeedGameInPlacePhaseAsync();

        var clientP1 = _factory.CreateClient();
        clientP1.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        var clientP2 = _factory.CreateClient();
        clientP2.DefaultRequestHeaders.Add("X-Bot-Token", tokenP2.ToString());

        // P1 places a piece on the board at edge (0, 0)
        await clientP1.PostAsJsonAsync($"/api/games/{game.Id}/place", new
        {
            action = "place",
            pieceId = p1Pieces[0].Id,
            position = new { row = 0, col = 0 }
        });

        // Act: P2 places a piece on the board at edge (7, 7)
        var response = await clientP2.PostAsJsonAsync($"/api/games/{game.Id}/place", new
        {
            action = "place",
            pieceId = p2Pieces[0].Id,
            position = new { row = 7, col = 7 }
        });
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert: phase advanced to MovePhase
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(json.RootElement.TryGetProperty("phase", out var phaseElement));
        Assert.Equal("MovePhase", phaseElement.GetString());
    }

    // ── Response shape when phase is still PlacePhase ─────────────────────────

    [Fact]
    public async Task Place_AfterOnlyP1Acts_PhaseIsStillPlacePhase()
    {
        // Arrange
        var (game, tokenP1, _, _, _) = await SeedGameInPlacePhaseAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act: only P1 skips; P2 has not acted yet
        var response = await client.PostAsJsonAsync($"/api/games/{game.Id}/place", new { action = "skip" });
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert: phase remains PlacePhase
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(json.RootElement.TryGetProperty("phase", out var phaseElement));
        Assert.Equal("PlacePhase", phaseElement.GetString());
    }

    // ── "place" action without required fields → 400 ──────────────────────────

    [Fact]
    public async Task Place_WithPlaceActionButMissingPieceId_Returns400()
    {
        // Arrange
        var (game, tokenP1, _, _, _) = await SeedGameInPlacePhaseAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // No pieceId provided
        var body = new { action = "place", position = new { row = 0, col = 0 } };

        // Act
        var response = await client.PostAsJsonAsync($"/api/games/{game.Id}/place", body);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Place_WithPlaceActionButMissingPosition_Returns400()
    {
        // Arrange
        var (game, tokenP1, _, p1Pieces, _) = await SeedGameInPlacePhaseAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // No position provided
        var body = new { action = "place", pieceId = p1Pieces[0].Id };

        // Act
        var response = await client.PostAsJsonAsync($"/api/games/{game.Id}/place", body);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
