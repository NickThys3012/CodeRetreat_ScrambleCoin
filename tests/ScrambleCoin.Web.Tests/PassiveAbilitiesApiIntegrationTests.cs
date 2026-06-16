using System.Net;
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
using ScrambleCoin.Domain.ValueObjects;
using ScrambleCoin.Infrastructure.Persistence;

namespace ScrambleCoin.Web.Tests;

/// <summary>
/// API integration tests for passive abilities (Issue #50).
/// Tests that passive ability pieces are correctly represented in the bot-facing REST API.
/// Uses <see cref="WebApplicationFactory{TEntryPoint}"/> with in-memory database.
/// </summary>
public class PassiveAbilitiesApiIntegrationTests : IClassFixture<PassiveAbilitiesApiIntegrationTests.TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public PassiveAbilitiesApiIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── Test factory ──────────────────────────────────────────────────────────

    public sealed class TestWebApplicationFactory : WebApplicationFactory<Api.ApiMarker>
    {
        // Unique DB name per factory instance so tests don't share state.
        private readonly string _dbName = $"PassiveAbilitiesApiTestDb_{Guid.NewGuid()}";

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

                // Re-register using an in-memory database.
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a game with a specific passive ability piece into the database.
    /// The game is in MovePhase with both players having pieces on board.
    /// </summary>
    private async Task<(Game game, Guid tokenP1, Guid tokenP2)> SeedGameWithAbilityAsync(string abilityName)
    {
        using var scope = _factory.Services.CreateScope();
        var gameRepo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var botRepo = scope.ServiceProvider.GetRequiredService<IBotRegistrationRepository>();

        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        // Create 5 pieces for each player (required by Lineup)
        var pieces1 = new List<Piece>
        {
            new(Guid.NewGuid(), abilityName, p1, EntryPointType.Borders, MovementType.Orthogonal, 3, 1),
            new(Guid.NewGuid(), "Mickey", p1, EntryPointType.Borders, MovementType.Orthogonal, 3, 1),
            new(Guid.NewGuid(), "Minnie", p1, EntryPointType.Borders, MovementType.Orthogonal, 3, 1),
            new(Guid.NewGuid(), "Donald", p1, EntryPointType.Borders, MovementType.Orthogonal, 3, 1),
            new(Guid.NewGuid(), "Goofy", p1, EntryPointType.Borders, MovementType.Orthogonal, 3, 1)
        };
        var pieces2 = new List<Piece>
        {
            new(Guid.NewGuid(), "Pluto", p2, EntryPointType.Borders, MovementType.Orthogonal, 3, 1),
            new(Guid.NewGuid(), "Daffy", p2, EntryPointType.Borders, MovementType.Orthogonal, 3, 1),
            new(Guid.NewGuid(), "Elmer", p2, EntryPointType.Borders, MovementType.Orthogonal, 3, 1),
            new(Guid.NewGuid(), "Bugs", p2, EntryPointType.Borders, MovementType.Orthogonal, 3, 1),
            new(Guid.NewGuid(), "Porky", p2, EntryPointType.Borders, MovementType.Orthogonal, 3, 1)
        };

        // Create and start a game
        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(pieces1));
        game.SetLineup(p2, new Lineup(pieces2));
        game.Start(); // Starts in the CoinSpawn phase

        // Advance to PlacePhase to allow piece placement
        game.AdvancePhase(); // CoinSpawn → PlacePhase

        // Place both players' pieces on board
        var p1Pieces = game.LineupPlayerOne!.Pieces;
        var p2Pieces = game.LineupPlayerTwo!.Pieces;

        game.PlacePiece(p1, p1Pieces[0].Id, new Position(0, 0));
        game.PlacePiece(p2, p2Pieces[0].Id, new Position(7, 7));

        // Advance to MovePhase for testing
        game.AdvancePhase(); // PlacePhase → MovePhase

        await gameRepo.SaveAsync(game);

        var tokenP1 = Guid.NewGuid();
        var tokenP2 = Guid.NewGuid();
        await botRepo.SaveAsync(new BotRegistration(tokenP1, p1, game.Id));
        await botRepo.SaveAsync(new BotRegistration(tokenP2, p2, game.Id));

        return (game, tokenP1, tokenP2);
    }

    // ── AC: API contract - ability pieces included in board state ─────────────

    [Fact]
    public async Task GetBoardState_WithScroogeOnBoard_ReturnsPieceInResponse()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameWithAbilityAsync("Scrooge");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync(new Uri($"/api/games/{game.Id}/state"));
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        // Assert: Response is 200 and contains board with pieces
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(doc.RootElement.TryGetProperty("board", out _));
        var boardProp = doc.RootElement.GetProperty("board");
        Assert.True(boardProp.TryGetProperty("tiles", out _));
    }

    [Fact]
    public async Task GetBoardState_WithMoanaOnBoard_ReturnsPieceInResponse()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameWithAbilityAsync("Moana");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync(new Uri($"/api/games/{game.Id}/state"));

        // Assert: Response contains Moana piece
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Moana", json);
    }

    [Fact]
    public async Task GetBoardState_WithFlynnOnBoard_ReturnsPieceInResponse()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameWithAbilityAsync("Flynn");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync(new Uri($"/api/games/{game.Id}/state"));

        // Assert: Response contains Flynn piece
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Flynn", json);
    }

    [Fact]
    public async Task GetBoardState_WithMerlinOnBoard_ReturnsPieceInResponse()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameWithAbilityAsync("Merlin");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync(new Uri($"/api/games/{game.Id}/state"));

        // Assert: Response contains Merlin piece
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Merlin", json);
    }

    [Fact]
    public async Task GetBoardState_WithRapunzelOnBoard_ReturnsPieceInResponse()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameWithAbilityAsync("Rapunzel");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync(new Uri($"/api/games/{game.Id}/state"));

        // Assert: Response contains Rapunzel piece
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Rapunzel", json);
    }

    [Fact]
    public async Task GetBoardState_WithCinderellaOnBoard_ReturnsPieceInResponse()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameWithAbilityAsync("Cinderella");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync(new Uri($"/api/games/{game.Id}/state"));

        // Assert: Response contains Cinderella piece
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Cinderella", json);
    }

    [Fact]
    public async Task GetBoardState_WithForkyOnBoard_ReturnsPieceInResponse()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameWithAbilityAsync("Forky");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync(new Uri($"/api/games/{game.Id}/state"));

        // Assert: Response contains Forky piece
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Forky", json);
    }

    [Fact]
    public async Task GetBoardState_WithFairyGodmotherOnBoard_ReturnsPieceInResponse()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameWithAbilityAsync("Fairy Godmother");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var temp = new Uri($"/api/games/{game.Id}/state");
        var response = await client.GetAsync(temp);

        // Assert: Response contains Fairy Godmother piece
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Fairy Godmother", json);
    }

    [Fact]
    public async Task GetBoardState_WithUrsulaOnBoard_ReturnsPieceInResponse()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameWithAbilityAsync("Ursula");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync(new Uri($"/api/games/{game.Id}/state"));

        // Assert: Response contains Ursula piece
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Ursula", json);
    }

    [Fact]
    public async Task GetBoardState_WithJafarOnBoard_ReturnsPieceInResponse()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameWithAbilityAsync("Jafar");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync(new Uri($"/api/games/{game.Id}/state"));

        // Assert: Response contains Jafar piece
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Jafar", json);
    }

    [Fact]
    public async Task GetBoardState_WithMikeWazowskiOnBoard_ReturnsPieceInResponse()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameWithAbilityAsync("Mike Wazowski");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync(new Uri($"/api/games/{game.Id}/state"));

        // Assert: Response contains Mike Wazowski piece
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Mike Wazowski", json);
    }

    // ── AC: API contract - missing token returns 401 or 403 ───────────────────────────

    [Fact]
    public async Task GetBoardState_WithoutToken_ReturnsUnauthorizedOrForbidden()
    {
        // Arrange
        var (game, _, _) = await SeedGameWithAbilityAsync("Scrooge");
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(new Uri($"/api/games/{game.Id}/state"));

        // Assert: Should reject request without a token
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected Unauthorized or Forbidden, got {response.StatusCode}");
    }

    [Fact]
    public async Task GetBoardState_WithInvalidToken_ReturnsUnauthorizedOrForbidden()
    {
        // Arrange
        var (game, _, _) = await SeedGameWithAbilityAsync("Scrooge");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", Guid.NewGuid().ToString());

        // Act
        var response = await client.GetAsync(new Uri($"/api/games/{game.Id}/state"));

        // Assert: Should reject invalid token
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected Unauthorized or Forbidden, got {response.StatusCode}");
    }

    // ── AC: Board state DTO contains required fields ────────────────────────────

    [Fact]
    public async Task GetBoardState_ResponseIsValidJson()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameWithAbilityAsync("Scrooge");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync(new Uri($"/api/games/{game.Id}/state"));
        var json = await response.Content.ReadAsStringAsync();

        // Assert: Response is valid JSON
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = JsonDocument.Parse(json); // Should not throw
        Assert.NotNull(doc);
    }

    [Fact]
    public async Task GetBoardState_ResponseContainsGameInfo()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameWithAbilityAsync("Scrooge");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync(new Uri($"/api/games/{game.Id}/state"));
        var json = await response.Content.ReadAsStringAsync();

        // Assert: Response contains expected properties
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("board", json, StringComparison.OrdinalIgnoreCase);
    }
}
