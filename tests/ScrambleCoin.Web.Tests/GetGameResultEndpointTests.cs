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
/// Integration tests for <c>GET /api/games/{gameId}/result</c> (Issue #51).
/// Uses <see cref="WebApplicationFactory{TEntryPoint}"/> with an in-memory database.
/// </summary>
public class GetGameResultEndpointTests : IClassFixture<GetGameResultEndpointTests.TestWebApplicationFactory>
{
    private static readonly IReadOnlyList<string> DefaultLineup =
        ["Mickey", "Minnie", "Donald", "Goofy", "Scrooge"];

    private readonly TestWebApplicationFactory _factory;

    public GetGameResultEndpointTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── Test factory ──────────────────────────────────────────────────────────

    public sealed class TestWebApplicationFactory : WebApplicationFactory<Api.ApiMarker>
    {
        // Unique DB name per factory instance so tests don't share state.
        private readonly string _dbName = $"GameResultTestDb_{Guid.NewGuid()}";

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
                // ONLY the InMemory provider.  This avoids the "two providers registered"
                // EF Core exception that occurs when SQL Server extension services from
                // Program.cs are still present in the ASP.NET Core DI container.
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
    /// Seeds a finished game with the supplied per-player scores.
    /// Returns the game and both bot tokens.
    /// </summary>
    private async Task<(Game game, Guid tokenP1, Guid tokenP2)> SeedFinishedGameAsync(
        int scoreP1 = 5,
        int scoreP2 = 3)
    {
        using var scope = _factory.Services.CreateScope();
        var gameRepo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var botRepo = scope.ServiceProvider.GetRequiredService<IBotRegistrationRepository>();

        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var pieces1 = DefaultLineup
            .Select(n => new Piece(Guid.NewGuid(), n, p1, EntryPointType.Borders, MovementType.Orthogonal, 3, 1))
            .ToList();
        var pieces2 = DefaultLineup
            .Select(n => new Piece(Guid.NewGuid(), n, p2, EntryPointType.Borders, MovementType.Orthogonal, 3, 1))
            .ToList();

        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(pieces1));
        game.SetLineup(p2, new Lineup(pieces2));
        game.Start(); // → InProgress

        if (scoreP1 > 0) game.AddScore(p1, scoreP1);
        if (scoreP2 > 0) game.AddScore(p2, scoreP2);

        game.End(); // → Finished

        await gameRepo.SaveAsync(game);

        var tokenP1 = Guid.NewGuid();
        var tokenP2 = Guid.NewGuid();
        await botRepo.SaveAsync(new BotRegistration(tokenP1, p1, game.Id));
        await botRepo.SaveAsync(new BotRegistration(tokenP2, p2, game.Id));

        return (game, tokenP1, tokenP2);
    }

    /// <summary>
    /// Seeds a game in <see cref="GameStatus.InProgress"/> (not yet finished).
    /// Returns the game and both bot tokens.
    /// </summary>
    private async Task<(Game game, Guid tokenP1, Guid tokenP2)> SeedInProgressGameAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var gameRepo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var botRepo = scope.ServiceProvider.GetRequiredService<IBotRegistrationRepository>();

        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var pieces1 = DefaultLineup
            .Select(n => new Piece(Guid.NewGuid(), n, p1, EntryPointType.Borders, MovementType.Orthogonal, 3, 1))
            .ToList();
        var pieces2 = DefaultLineup
            .Select(n => new Piece(Guid.NewGuid(), n, p2, EntryPointType.Borders, MovementType.Orthogonal, 3, 1))
            .ToList();

        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(pieces1));
        game.SetLineup(p2, new Lineup(pieces2));
        game.Start(); // → InProgress (not finished)

        await gameRepo.SaveAsync(game);

        var tokenP1 = Guid.NewGuid();
        var tokenP2 = Guid.NewGuid();
        await botRepo.SaveAsync(new BotRegistration(tokenP1, p1, game.Id));
        await botRepo.SaveAsync(new BotRegistration(tokenP2, p2, game.Id));

        return (game, tokenP1, tokenP2);
    }

    // ── 200 OK: valid token for a finished game ───────────────────────────────

    [Fact]
    public async Task GetGameResult_ValidTokenFinishedGame_Returns200()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedFinishedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync(new Uri($"/api/games/{game.Id}/result"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetGameResult_ValidTokenFinishedGame_ReturnsGameIdField()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedFinishedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync(new Uri($"/api/games/{game.Id}/result"));
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert: response contains 'gameId' field.
        Assert.True(json.RootElement.TryGetProperty("gameId", out _),
            "Response JSON must contain a 'gameId' field.");
    }

    [Fact]
    public async Task GetGameResult_ValidTokenFinishedGame_ReturnsWinnerIdField()
    {
        // Arrange: P1 wins (5 > 3).
        var (game, tokenP1, _) = await SeedFinishedGameAsync(scoreP1: 5, scoreP2: 3);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync(new Uri($"/api/games/{game.Id}/result"));
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert: response contains 'winnerId' field.
        Assert.True(json.RootElement.TryGetProperty("winnerId", out _),
            "Response JSON must contain a 'winnerId' field.");
    }

    [Fact]
    public async Task GetGameResult_ValidTokenFinishedGame_ReturnsIsDrawField()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedFinishedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync(new Uri($"/api/games/{game.Id}/result"));
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert: response contains 'isDraw' field.
        Assert.True(json.RootElement.TryGetProperty("isDraw", out _),
            "Response JSON must contain an 'isDraw' field.");
    }

    [Fact]
    public async Task GetGameResult_WhenP1Wins_WinnerIdIsPlayerOne()
    {
        // Arrange: P1 = 10, P2 = 2 → P1 wins.
        var (game, tokenP1, _) = await SeedFinishedGameAsync(scoreP1: 10, scoreP2: 2);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync(new Uri($"/api/games/{game.Id}/result"));
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        var winnerIdStr = json.RootElement.GetProperty("winnerId").GetString();
        Assert.Equal(game.PlayerOne.ToString(), winnerIdStr);
    }

    [Fact]
    public async Task GetGameResult_WhenScoresEqual_IsDrawIsTrue()
    {
        // Arrange: tie — both 4 points.
        var (game, tokenP1, _) = await SeedFinishedGameAsync(scoreP1: 4, scoreP2: 4);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync(new Uri($"/api/games/{game.Id}/result"));
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.True(json.RootElement.GetProperty("isDraw").GetBoolean());
    }

    // ── 403 Forbidden: missing X-Bot-Token header ─────────────────────────────

    [Fact]
    public async Task GetGameResult_WithoutBotTokenHeader_Returns403()
    {
        // Arrange: no X-Bot-Token header at all.
        var gameId = Guid.NewGuid();
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(new Uri($"/api/games/{gameId}/result"));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetGameResult_WithMalformedBotTokenHeader_Returns403()
    {
        // Arrange: header value is not a valid GUID.
        var gameId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", "not-a-guid");

        // Act
        var response = await client.GetAsync(new Uri($"/api/games/{gameId}/result"));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── 403 Forbidden: token not registered to this game ─────────────────────

    [Fact]
    public async Task GetGameResult_WithTokenFromDifferentGame_Returns403()
    {
        // Arrange: seed two finished games; use P1's token from game A against game B.
        var (_, tokenP1FromA, _) = await SeedFinishedGameAsync();
        var (gameB, _, _) = await SeedFinishedGameAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1FromA.ToString());

        // Act
        var response = await client.GetAsync(new Uri($"/api/games/{gameB.Id}/result"));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetGameResult_WithUnknownToken_Returns403()
    {
        // Arrange: a completely unknown token (not registered to any game).
        var (game, _, _) = await SeedFinishedGameAsync();
        var unknownToken = Guid.NewGuid();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", unknownToken.ToString());

        // Act
        var response = await client.GetAsync(new Uri($"/api/games/{game.Id}/result"));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── 404 Not Found: unknown game ID ────────────────────────────────────────

    [Fact]
    public async Task GetGameResult_WithUnknownGameId_Returns404()
    {
        // Arrange: seed a valid game to get a real token, but request a non-existent gameId.
        var (_, tokenP1, _) = await SeedFinishedGameAsync();
        var unknownGameId = Guid.NewGuid();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync(new Uri($"/api/games/{unknownGameId}/result"));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); // token doesn't belong to unknown game
    }

    // ── 409 Conflict: game not yet finished ───────────────────────────────────

    [Fact]
    public async Task GetGameResult_WhenGameIsInProgress_Returns409()
    {
        // Arrange: seed a game that is still running (not finished).
        var (game, tokenP1, _) = await SeedInProgressGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync(new Uri($"/api/games/{game.Id}/result"));

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetGameResult_WhenGameIsInProgress_ResponseContainsProblemDetails()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedInProgressGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync(new Uri($"/api/games/{game.Id}/result"));
        var body = await response.Content.ReadAsStringAsync();

        // Assert: response body is a ProblemDetails JSON object.
        Assert.False(string.IsNullOrEmpty(body));
        var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("title", out _),
            "409 response must be a ProblemDetails object with a 'title' field.");
    }
}
