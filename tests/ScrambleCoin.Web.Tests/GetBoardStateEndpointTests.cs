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
using ScrambleCoin.Domain.ValueObjects;
using ScrambleCoin.Infrastructure.Persistence;

namespace ScrambleCoin.Web.Tests;

/// <summary>
/// Integration tests for <c>GET /api/games/{gameId}/state</c> (Issue #38).
/// Uses <see cref="WebApplicationFactory{TEntryPoint}"/> with an in-memory database.
/// </summary>
public class GetBoardStateEndpointTests : IClassFixture<GetBoardStateEndpointTests.TestWebApplicationFactory>
{
    private static readonly IReadOnlyList<string> DefaultLineup =
        ["Mickey", "Minnie", "Donald", "Goofy", "Scrooge"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly TestWebApplicationFactory _factory;

    public GetBoardStateEndpointTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── Test factory ──────────────────────────────────────────────────────────

    public sealed class TestWebApplicationFactory : WebApplicationFactory<ScrambleCoin.Api.ApiMarker>
    {
        // Unique DB name per factory instance so tests don't share state.
        private readonly string _dbName = $"BoardStateTestDb_{Guid.NewGuid()}";

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

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a started game (CoinSpawn phase) plus two bot registrations into
    /// the in-memory database. Returns the game and both tokens.
    /// </summary>
    private async Task<(Game game, Guid tokenP1, Guid tokenP2)> SeedGameAsync()
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
        game.Start();

        await gameRepo.SaveAsync(game);

        var tokenP1 = Guid.NewGuid();
        var tokenP2 = Guid.NewGuid();
        await botRepo.SaveAsync(new BotRegistration(tokenP1, p1, game.Id));
        await botRepo.SaveAsync(new BotRegistration(tokenP2, p2, game.Id));

        return (game, tokenP1, tokenP2);
    }

    // ── AC 1 : Returns 200 with JSON body ────────────────────────────────────

    [Fact]
    public async Task GetBoardState_WithValidToken_Returns200()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetBoardState_WithValidToken_ReturnsJsonContentType()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");

        // Assert
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.Contains("application/json", response.Content.Headers.ContentType!.MediaType);
    }

    // ── AC 2 : Response body has correct shape ────────────────────────────────

    [Fact]
    public async Task GetBoardState_Response_ContainsTurnField()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.True(json.RootElement.TryGetProperty("turn", out _),
            "Response JSON must contain a 'turn' field.");
    }

    [Fact]
    public async Task GetBoardState_Response_ContainsPhaseField()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.True(json.RootElement.TryGetProperty("phase", out _),
            "Response JSON must contain a 'phase' field.");
    }

    [Fact]
    public async Task GetBoardState_Response_ContainsYourScoreField()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.True(json.RootElement.TryGetProperty("yourScore", out _),
            "Response JSON must contain a 'yourScore' field.");
    }

    [Fact]
    public async Task GetBoardState_Response_ContainsOpponentScoreField()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.True(json.RootElement.TryGetProperty("opponentScore", out _),
            "Response JSON must contain an 'opponentScore' field.");
    }

    [Fact]
    public async Task GetBoardState_Response_ContainsBoardWithTilesField()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.True(json.RootElement.TryGetProperty("board", out var board),
            "Response JSON must contain a 'board' field.");
        Assert.True(board.TryGetProperty("tiles", out _),
            "board must contain a 'tiles' array.");
    }

    [Fact]
    public async Task GetBoardState_Response_ContainsYourPiecesField()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.True(json.RootElement.TryGetProperty("yourPieces", out _),
            "Response JSON must contain a 'yourPieces' field.");
    }

    [Fact]
    public async Task GetBoardState_Response_ContainsOpponentPiecesField()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.True(json.RootElement.TryGetProperty("opponentPieces", out _),
            "Response JSON must contain an 'opponentPieces' field.");
    }

    [Fact]
    public async Task GetBoardState_Response_ContainsAvailableCoinsField()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.True(json.RootElement.TryGetProperty("availableCoins", out _),
            "Response JSON must contain an 'availableCoins' field.");
    }

    [Fact]
    public async Task GetBoardState_Response_ContainsActivePlayerField()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert: activePlayer key must be present (may be null)
        Assert.True(json.RootElement.TryGetProperty("activePlayer", out _),
            "Response JSON must contain an 'activePlayer' field (may be null).");
    }

    // ── AC 3 : Tiles have correct shape ──────────────────────────────────────

    [Fact]
    public async Task GetBoardState_Board_Contains64Tiles()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        var tiles = json.RootElement.GetProperty("board").GetProperty("tiles");
        Assert.Equal(64, tiles.GetArrayLength());
    }

    [Fact]
    public async Task GetBoardState_EachTile_ContainsPositionField()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert: all tiles have 'position' with 'row' and 'col'
        var tiles = json.RootElement.GetProperty("board").GetProperty("tiles");
        foreach (var tile in tiles.EnumerateArray())
        {
            Assert.True(tile.TryGetProperty("position", out var pos));
            Assert.True(pos.TryGetProperty("row", out _));
            Assert.True(pos.TryGetProperty("col", out _));
        }
    }

    [Fact]
    public async Task GetBoardState_EachTile_ContainsIsObstacleField()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        var tiles = json.RootElement.GetProperty("board").GetProperty("tiles");
        foreach (var tile in tiles.EnumerateArray())
            Assert.True(tile.TryGetProperty("isObstacle", out _));
    }

    [Fact]
    public async Task GetBoardState_EachTile_ContainsFencedEdgesField()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        var tiles = json.RootElement.GetProperty("board").GetProperty("tiles");
        foreach (var tile in tiles.EnumerateArray())
            Assert.True(tile.TryGetProperty("fencedEdges", out _));
    }

    // ── AC 4 : Pieces have correct shape ──────────────────────────────────────

    [Fact]
    public async Task GetBoardState_YourPieces_ContainsLineupCount()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        var pieces = json.RootElement.GetProperty("yourPieces");
        Assert.Equal(DefaultLineup.Count, pieces.GetArrayLength());
    }

    [Fact]
    public async Task GetBoardState_EachPiece_HasPieceIdField()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        foreach (var piece in json.RootElement.GetProperty("yourPieces").EnumerateArray())
            Assert.True(piece.TryGetProperty("pieceId", out _));
    }

    [Fact]
    public async Task GetBoardState_EachPiece_HasNameField()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        foreach (var piece in json.RootElement.GetProperty("yourPieces").EnumerateArray())
            Assert.True(piece.TryGetProperty("name", out _));
    }

    [Fact]
    public async Task GetBoardState_EachPiece_HasMovementTypeField()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        foreach (var piece in json.RootElement.GetProperty("yourPieces").EnumerateArray())
            Assert.True(piece.TryGetProperty("movementType", out _));
    }

    [Fact]
    public async Task GetBoardState_EachPiece_HasMaxDistanceField()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        foreach (var piece in json.RootElement.GetProperty("yourPieces").EnumerateArray())
            Assert.True(piece.TryGetProperty("maxDistance", out _));
    }

    [Fact]
    public async Task GetBoardState_EachPiece_HasIsOnBoardField()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        foreach (var piece in json.RootElement.GetProperty("yourPieces").EnumerateArray())
            Assert.True(piece.TryGetProperty("isOnBoard", out _));
    }

    // ── AC 5 : 404 when game not found ────────────────────────────────────────

    [Fact]
    public async Task GetBoardState_WhenTokenBelongsToADifferentGame_Returns403()
    {
        // Arrange: valid token format but game doesn't exist
        // We need a real token; seed a game to get one then query a different ID
        var (_, tokenP1, _) = await SeedGameAsync();
        var nonExistentGameId = Guid.NewGuid();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act: the token is for the seeded game, but we query a completely different game ID
        // This should trigger 403 (token belongs to wrong game) — see note below.
        // To get a clean 404, we query with a token that belongs to no registered game.
        var unknownToken = Guid.NewGuid();
        // First: a genuinely unknown game ID with the seeded token causes 403.
        // A 404 happens when the token IS valid for a game but the game record is deleted.
        // To keep this test deterministic, we directly test via the handler behavior:
        // endpoint returns 404 when game is not found. Here we verify via an unknown token → 403.
        // The 404 case is better covered in the unit test for the handler.
        // Nonetheless: call with an entirely fabricated gameId and valid registered token.
        client.DefaultRequestHeaders.Remove("X-Bot-Token");
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());
        var response = await client.GetAsync($"/api/games/{nonExistentGameId}/state");

        // The token is registered for a different game, so we get 403 (auth check fires first).
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetBoardState_WithUnregisteredToken_Returns403WithProblemDetails()
    {
        // Arrange
        var (game, _, _) = await SeedGameAsync();
        var unknownToken = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", unknownToken.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetBoardState_WithUnregisteredToken_ResponseBodyHasProblemDetails()
    {
        // Arrange
        var (game, _, _) = await SeedGameAsync();
        var unknownToken = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", unknownToken.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert: ProblemDetails must have 'title' and 'status'
        Assert.True(json.RootElement.TryGetProperty("title", out _),
            "403 response must include ProblemDetails 'title'.");
        Assert.True(json.RootElement.TryGetProperty("status", out var status));
        Assert.Equal(403, status.GetInt32());
    }

    // ── AC 6 : 403 when X-Bot-Token header is missing or invalid ─────────────

    [Fact]
    public async Task GetBoardState_WithMissingToken_Returns403()
    {
        // Arrange: no X-Bot-Token header
        var (game, _, _) = await SeedGameAsync();
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetBoardState_WithMissingToken_ResponseBodyHasProblemDetails()
    {
        // Arrange
        var (game, _, _) = await SeedGameAsync();
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert: must be a ProblemDetails body
        Assert.True(json.RootElement.TryGetProperty("status", out var status));
        Assert.Equal(403, status.GetInt32());
    }

    [Fact]
    public async Task GetBoardState_WithNonGuidToken_Returns403()
    {
        // Arrange: token that is not a valid GUID
        var (game, _, _) = await SeedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", "not-a-guid");

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── AC 7 : yourPieces / opponentPieces are caller-relative ───────────────

    [Fact]
    public async Task GetBoardState_BothPlayersQuery_YourPiecesAreDifferentPerCaller()
    {
        // Arrange
        var (game, tokenP1, tokenP2) = await SeedGameAsync();
        var clientP1 = _factory.CreateClient();
        clientP1.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        var clientP2 = _factory.CreateClient();
        clientP2.DefaultRequestHeaders.Add("X-Bot-Token", tokenP2.ToString());

        // Act
        var responseP1 = await clientP1.GetAsync($"/api/games/{game.Id}/state");
        var responseP2 = await clientP2.GetAsync($"/api/games/{game.Id}/state");

        var jsonP1 = JsonDocument.Parse(await responseP1.Content.ReadAsStringAsync());
        var jsonP2 = JsonDocument.Parse(await responseP2.Content.ReadAsStringAsync());

        // Assert: pieces reported as "yours" for P1 != pieces reported as "yours" for P2
        var p1YourIds = jsonP1.RootElement.GetProperty("yourPieces")
            .EnumerateArray()
            .Select(p => p.GetProperty("pieceId").GetString())
            .OrderBy(x => x)
            .ToList();

        var p2YourIds = jsonP2.RootElement.GetProperty("yourPieces")
            .EnumerateArray()
            .Select(p => p.GetProperty("pieceId").GetString())
            .OrderBy(x => x)
            .ToList();

        Assert.NotEqual(p1YourIds, p2YourIds);
    }

    [Fact]
    public async Task GetBoardState_P1Query_YourPiecesMatchesP1Lineup_AndOpponentPiecesMatchesP2Lineup()
    {
        // Arrange
        var (game, tokenP1, tokenP2) = await SeedGameAsync();

        var clientP1 = _factory.CreateClient();
        clientP1.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        var clientP2 = _factory.CreateClient();
        clientP2.DefaultRequestHeaders.Add("X-Bot-Token", tokenP2.ToString());

        // Act: get the state from P1's perspective and from P2's perspective
        var p1Response = await clientP1.GetAsync($"/api/games/{game.Id}/state");
        var p2Response = await clientP2.GetAsync($"/api/games/{game.Id}/state");

        var p1Json = JsonDocument.Parse(await p1Response.Content.ReadAsStringAsync());
        var p2Json = JsonDocument.Parse(await p2Response.Content.ReadAsStringAsync());

        // P1's "yourPieces" should equal P2's "opponentPieces"
        var p1YourIds = p1Json.RootElement.GetProperty("yourPieces")
            .EnumerateArray()
            .Select(p => p.GetProperty("pieceId").GetString())
            .OrderBy(x => x)
            .ToList();

        var p2OpponentIds = p2Json.RootElement.GetProperty("opponentPieces")
            .EnumerateArray()
            .Select(p => p.GetProperty("pieceId").GetString())
            .OrderBy(x => x)
            .ToList();

        Assert.Equal(p1YourIds, p2OpponentIds);
    }

    // ── AC 8 : availableCoins only includes coin tiles ─────────────────────────

    [Fact]
    public async Task GetBoardState_NoCoinsSpawned_AvailableCoinsIsEmpty()
    {
        // Arrange: game just started, no coins
        var (game, tokenP1, _) = await SeedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        var coins = json.RootElement.GetProperty("availableCoins");
        Assert.Equal(0, coins.GetArrayLength());
    }

    [Fact]
    public async Task GetBoardState_WithSpawnedCoins_AvailableCoinsContainsCoinData()
    {
        // Arrange: seed a game and spawn coins
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
        game.Start();
        game.SpawnCoins(new[]
        {
            (new Position(0, 0), CoinType.Silver),
            (new Position(7, 7), CoinType.Gold)
        });

        await gameRepo.SaveAsync(game);

        var tokenP1 = Guid.NewGuid();
        await botRepo.SaveAsync(new BotRegistration(tokenP1, p1, game.Id));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        var coins = json.RootElement.GetProperty("availableCoins");
        Assert.Equal(2, coins.GetArrayLength());
    }

    [Fact]
    public async Task GetBoardState_WithSpawnedCoins_EachCoinHasPositionCoinTypeAndValue()
    {
        // Arrange
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
        game.Start();
        game.SpawnCoins(new[] { (new Position(3, 3), CoinType.Gold) });

        await gameRepo.SaveAsync(game);
        var tokenP1 = Guid.NewGuid();
        await botRepo.SaveAsync(new BotRegistration(tokenP1, p1, game.Id));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert: the single coin has the required fields
        var coin = json.RootElement.GetProperty("availableCoins").EnumerateArray().First();
        Assert.True(coin.TryGetProperty("position", out _), "coin must have 'position'");
        Assert.True(coin.TryGetProperty("coinType", out var coinType), "coin must have 'coinType'");
        Assert.True(coin.TryGetProperty("value", out var value), "coin must have 'value'");
        Assert.Equal("Gold", coinType.GetString());
        Assert.Equal(3, value.GetInt32());
    }

    // ── Turn / score correctness ──────────────────────────────────────────────

    [Fact]
    public async Task GetBoardState_Response_TurnIsOne_AfterGameStart()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(1, json.RootElement.GetProperty("turn").GetInt32());
    }

    [Fact]
    public async Task GetBoardState_Response_PhaseIsCoinSpawn_AfterGameStart()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal("CoinSpawn", json.RootElement.GetProperty("phase").GetString());
    }

    // ── AC 5 : True 404 when game row is deleted ──────────────────────────────

    [Fact]
    public async Task GetBoardState_UnknownGameId_Returns404()
    {
        // Arrange: seed a game so we have a valid bot registration, then delete the game row
        var (game, tokenP1, _) = await SeedGameAsync();

        // Delete the game record from the in-memory database so the repository throws GameNotFoundException
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ScrambleCoinDbContext>();
            var record = await db.Games.FindAsync(game.Id);
            if (record is not null)
            {
                db.Games.Remove(record);
                await db.SaveChangesAsync();
            }
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act: token is valid and belongs to the (now-deleted) game → handler loads game → GameNotFoundException → 404
        var response = await client.GetAsync($"/api/games/{game.Id}/state");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.TryGetProperty("status", out var status),
            "404 response must include ProblemDetails 'status' field.");
        Assert.Equal(404, status.GetInt32());
    }

    // ── movesPerTurn field on pieces ──────────────────────────────────────────

    [Fact]
    public async Task GetBoardState_EachPiece_HasMovesPerTurnField()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert: every piece in yourPieces has a 'movesPerTurn' JSON property
        var pieces = json.RootElement.GetProperty("yourPieces");
        foreach (var piece in pieces.EnumerateArray())
        {
            Assert.True(piece.TryGetProperty("movesPerTurn", out var movesPerTurn),
                "Each piece must expose a 'movesPerTurn' JSON field.");
            Assert.True(movesPerTurn.GetInt32() >= 1,
                $"movesPerTurn must be >= 1, got {movesPerTurn.GetInt32()}.");
        }
    }

    // ── occupant key on every tile ────────────────────────────────────────────

    [Fact]
    public async Task GetBoardState_EachTile_HasOccupantKey()
    {
        // Arrange
        var (game, tokenP1, _) = await SeedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id}/state");
        var rawJson = await response.Content.ReadAsStringAsync();

        // Use JsonIgnoreCondition.Never-aware options so we can detect null-valued keys.
        // Parse with standard options; the production API must include the key even when null.
        var json = JsonDocument.Parse(rawJson);

        // Assert: every tile in board.tiles has an 'occupant' key (value may be null)
        var tiles = json.RootElement.GetProperty("board").GetProperty("tiles");
        foreach (var tile in tiles.EnumerateArray())
        {
            Assert.True(tile.TryGetProperty("occupant", out _),
                "Each tile must expose an 'occupant' JSON key (value may be null, but the key must be present).");
        }
    }
}
