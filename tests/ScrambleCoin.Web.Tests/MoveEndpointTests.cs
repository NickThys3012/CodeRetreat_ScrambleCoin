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
/// Integration tests for <c>POST /api/games/{gameId}/move</c> (Issue #40).
/// Uses <see cref="WebApplicationFactory{TEntryPoint}"/> with an in-memory database.
/// </summary>
public class MoveEndpointTests : IClassFixture<MoveEndpointTests.TestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly TestWebApplicationFactory _factory;

    public MoveEndpointTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── Test factory ──────────────────────────────────────────────────────────

    public sealed class TestWebApplicationFactory : WebApplicationFactory<Api.ApiMarker>
    {
        // Unique DB name per factory instance so test classes don't share state.
        private readonly string _dbName = $"MoveTestDb_{Guid.NewGuid()}";

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
    /// Seeds a game in <see cref="TurnPhase.MovePhase"/> where P1 has one piece at (0,0)
    /// and P2 has no pieces on board (P2 skipped placement).
    /// Returns (game, tokenP1, tokenP2, p1PieceId).
    /// </summary>
    private async Task<(Game game, Guid tokenP1, Guid tokenP2, Guid p1PieceId)>
        SeedGameInMovePhaseWithP1PieceAsync()
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

        // P1 places piece at corner (0, 0); P2 skips → auto-advances to MovePhase
        game.PlacePiece(p1, p1Pieces[0].Id, new Position(0, 0));
        game.SkipPlacement(p2); // → MovePhase

        await gameRepo.SaveAsync(game);

        var tokenP1 = Guid.NewGuid();
        var tokenP2 = Guid.NewGuid();
        await botRepo.SaveAsync(new BotRegistration(tokenP1, p1, game.Id));
        await botRepo.SaveAsync(new BotRegistration(tokenP2, p2, game.Id));

        return (game, tokenP1, tokenP2, p1Pieces[0].Id);
    }

    /// <summary>
    /// Seeds a game in <see cref="TurnPhase.MovePhase"/> where both players have one piece
    /// on the board: P1 at (0,0) and P2 at (7,7).
    /// Returns (game, tokenP1, tokenP2, p1PieceId, p2PieceId).
    /// </summary>
    private async Task<(Game game, Guid tokenP1, Guid tokenP2, Guid p1PieceId, Guid p2PieceId)>
        SeedGameInMovePhaseWithBothPiecesAsync()
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

        // Both players place a piece → auto-advances to MovePhase
        game.PlacePiece(p1, p1Pieces[0].Id, new Position(0, 0));
        game.PlacePiece(p2, p2Pieces[0].Id, new Position(7, 7));

        await gameRepo.SaveAsync(game);

        var tokenP1 = Guid.NewGuid();
        var tokenP2 = Guid.NewGuid();
        await botRepo.SaveAsync(new BotRegistration(tokenP1, p1, game.Id));
        await botRepo.SaveAsync(new BotRegistration(tokenP2, p2, game.Id));

        return (game, tokenP1, tokenP2, p1Pieces[0].Id, p2Pieces[0].Id);
    }

    /// <summary>
    /// Seeds a game in <see cref="TurnPhase.MovePhase"/> where P1 has one piece at (0,0)
    /// and a silver coin is sitting at (0,1) — the tile P1's piece will step onto.
    /// Returns (game, tokenP1, tokenP2, p1PieceId).
    /// </summary>
    private async Task<(Game game, Guid tokenP1, Guid tokenP2, Guid p1PieceId)>
        SeedGameInMovePhaseWithCoinAtTargetAsync()
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
        game.Start(); // → CoinSpawn Turn 1

        // Spawn a silver coin at (0,1) — right next to where P1's piece will be placed.
        game.SpawnCoins(new[] { (new Position(0, 1), CoinType.Silver) });

        game.AdvancePhase();     // → PlacePhase Turn 1

        // P1 places piece at (0,0); P2 skips → auto-advances to MovePhase
        game.PlacePiece(p1, p1Pieces[0].Id, new Position(0, 0));
        game.SkipPlacement(p2); // → MovePhase

        await gameRepo.SaveAsync(game);

        var tokenP1 = Guid.NewGuid();
        var tokenP2 = Guid.NewGuid();
        await botRepo.SaveAsync(new BotRegistration(tokenP1, p1, game.Id));
        await botRepo.SaveAsync(new BotRegistration(tokenP2, p2, game.Id));

        return (game, tokenP1, tokenP2, p1Pieces[0].Id);
    }

    /// <summary>
    /// Seeds a game in <see cref="TurnPhase.PlacePhase"/> (Turn 1), two bot registrations.
    /// Returns (game, tokenP1, tokenP2).
    /// </summary>
    private async Task<(Game game, Guid tokenP1, Guid tokenP2)> SeedGameInPlacePhaseAsync()
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
        game.Start();        // → CoinSpawn Turn 1
        game.AdvancePhase(); // → PlacePhase Turn 1

        await gameRepo.SaveAsync(game);

        var tokenP1 = Guid.NewGuid();
        var tokenP2 = Guid.NewGuid();
        await botRepo.SaveAsync(new BotRegistration(tokenP1, p1, game.Id));
        await botRepo.SaveAsync(new BotRegistration(tokenP2, p2, game.Id));

        return (game, tokenP1, tokenP2);
    }

    // ── AC 1 : valid token + valid move during MovePhase → 200 OK ─────────────

    [Fact]
    public async Task Move_ValidTokenAndMove_Returns200()
    {
        // Arrange: game in MovePhase with P1's piece at (0,0)
        var (game, tokenP1, _, p1PieceId) = await SeedGameInMovePhaseWithP1PieceAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        var body = new
        {
            pieceId = p1PieceId,
            segments = new[] { new[] { new { row = 0, col = 1 } } }
        };

        // Act
        var response = await client.PostAsJsonAsync($"/api/games/{game.Id}/move", body);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Move_ValidMove_ResponseContainsPhaseField()
    {
        // Arrange
        var (game, tokenP1, _, p1PieceId) = await SeedGameInMovePhaseWithP1PieceAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        var body = new
        {
            pieceId = p1PieceId,
            segments = new[] { new[] { new { row = 0, col = 1 } } }
        };

        // Act
        var response = await client.PostAsJsonAsync($"/api/games/{game.Id}/move", body);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.True(json.RootElement.TryGetProperty("phase", out _),
            "Response JSON must contain a 'phase' field.");
    }

    [Fact]
    public async Task Move_ValidMove_ResponseContainsActivePlayerField()
    {
        // Arrange
        var (game, tokenP1, _, p1PieceId) = await SeedGameInMovePhaseWithP1PieceAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        var body = new
        {
            pieceId = p1PieceId,
            segments = new[] { new[] { new { row = 0, col = 1 } } }
        };

        // Act
        var response = await client.PostAsJsonAsync($"/api/games/{game.Id}/move", body);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.True(json.RootElement.TryGetProperty("activePlayer", out _),
            "Response JSON must contain an 'activePlayer' field (may be null).");
    }

    [Fact]
    public async Task Move_ValidMove_ResponseContainsYourScoreField()
    {
        // Arrange
        var (game, tokenP1, _, p1PieceId) = await SeedGameInMovePhaseWithP1PieceAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        var body = new
        {
            pieceId = p1PieceId,
            segments = new[] { new[] { new { row = 0, col = 1 } } }
        };

        // Act
        var response = await client.PostAsJsonAsync($"/api/games/{game.Id}/move", body);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.True(json.RootElement.TryGetProperty("yourScore", out _),
            "Response JSON must contain a 'yourScore' field.");
    }

    [Fact]
    public async Task Move_ValidMove_ResponseContainsOpponentScoreField()
    {
        // Arrange
        var (game, tokenP1, _, p1PieceId) = await SeedGameInMovePhaseWithP1PieceAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        var body = new
        {
            pieceId = p1PieceId,
            segments = new[] { new[] { new { row = 0, col = 1 } } }
        };

        // Act
        var response = await client.PostAsJsonAsync($"/api/games/{game.Id}/move", body);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.True(json.RootElement.TryGetProperty("opponentScore", out _),
            "Response JSON must contain an 'opponentScore' field.");
    }

    // ── AC 2 : missing X-Bot-Token header → 403 ──────────────────────────────

    [Fact]
    public async Task Move_WithoutBotTokenHeader_Returns403()
    {
        // Arrange: game in MovePhase, no token header added
        var (game, _, _, p1PieceId) = await SeedGameInMovePhaseWithP1PieceAsync();
        var client = _factory.CreateClient();
        // No X-Bot-Token header

        var body = new
        {
            pieceId = p1PieceId,
            segments = new[] { new[] { new { row = 0, col = 1 } } }
        };

        // Act
        var response = await client.PostAsJsonAsync($"/api/games/{game.Id}/move", body);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── AC 3 : malformed (non-GUID) X-Bot-Token → 403 ────────────────────────

    [Fact]
    public async Task Move_WithMalformedBotToken_Returns403()
    {
        // Arrange: token is not a valid GUID
        var (game, _, _, p1PieceId) = await SeedGameInMovePhaseWithP1PieceAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", "not-a-valid-guid");

        var body = new
        {
            pieceId = p1PieceId,
            segments = new[] { new[] { new { row = 0, col = 1 } } }
        };

        // Act
        var response = await client.PostAsJsonAsync($"/api/games/{game.Id}/move", body);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── AC 4 : valid token but for a different game → 403 ────────────────────

    [Fact]
    public async Task Move_WithTokenFromDifferentGame_Returns403()
    {
        // Arrange: seed two separate games; use tokenP1 from game1 against game2
        var (_, tokenFromOtherGame, _, _) = await SeedGameInMovePhaseWithP1PieceAsync();
        var (game2, _, _, p1PieceId) = await SeedGameInMovePhaseWithP1PieceAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenFromOtherGame.ToString());

        var body = new
        {
            pieceId = p1PieceId,
            segments = new[] { new[] { new { row = 0, col = 1 } } }
        };

        // Act: present a valid token, but it belongs to a different game
        var response = await client.PostAsJsonAsync($"/api/games/{game2.Id}/move", body);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── AC 5 : move during PlacePhase (wrong phase) → 400 ────────────────────

    [Fact]
    public async Task Move_DuringPlacePhase_Returns400()
    {
        // Arrange: game has NOT yet advanced to MovePhase
        var (game, tokenP1, _) = await SeedGameInPlacePhaseAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        var body = new
        {
            pieceId = Guid.NewGuid(),
            segments = new[] { new[] { new { row = 0, col = 1 } } }
        };

        // Act
        var response = await client.PostAsJsonAsync($"/api/games/{game.Id}/move", body);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── AC 6 : move when it's not your turn → 400 ────────────────────────────

    [Fact]
    public async Task Move_WhenNotYourTurn_Returns400()
    {
        // Arrange: game in MovePhase with both players having one piece on the board.
        // MovePhaseActivePlayer == PlayerOne, so P2 should be rejected.
        var (game, _, tokenP2, _, p2PieceId) = await SeedGameInMovePhaseWithBothPiecesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP2.ToString());

        var body = new
        {
            pieceId = p2PieceId,
            segments = new[] { new[] { new { row = 7, col = 6 } } }
        };

        // Act: P2 tries to move before P1 has made any move
        var response = await client.PostAsJsonAsync($"/api/games/{game.Id}/move", body);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── AC 7 : body.Segments is null → 400 ───────────────────────────────────

    [Fact]
    public async Task Move_WithNullSegments_Returns400()
    {
        // Arrange: valid token and game, but segments field is omitted (null)
        var (game, tokenP1, _, p1PieceId) = await SeedGameInMovePhaseWithP1PieceAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Omit segments — deserialises as null
        var body = new { pieceId = p1PieceId };

        // Act
        var response = await client.PostAsJsonAsync($"/api/games/{game.Id}/move", body);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── AC 8 : move through a coin tile → yourScore incremented ──────────────

    [Fact]
    public async Task Move_ThroughCoinTile_ReturnsIncrementedYourScore()
    {
        // Arrange: game in MovePhase with P1's piece at (0,0) and a silver coin at (0,1).
        // Silver coin value = 1.
        var (game, tokenP1, _, p1PieceId) = await SeedGameInMovePhaseWithCoinAtTargetAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        var body = new
        {
            pieceId = p1PieceId,
            segments = new[] { new[] { new { row = 0, col = 1 } } } // steps onto the coin tile
        };

        // Act
        var response = await client.PostAsJsonAsync($"/api/games/{game.Id}/move", body);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(json.RootElement.TryGetProperty("yourScore", out var yourScore));
        Assert.Equal(1, yourScore.GetInt32()); // Silver coin = 1 point
    }

    [Fact]
    public async Task Move_ThroughCoinTile_OpponentScoreUnchanged()
    {
        // Arrange
        var (game, tokenP1, _, p1PieceId) = await SeedGameInMovePhaseWithCoinAtTargetAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        var body = new
        {
            pieceId = p1PieceId,
            segments = new[] { new[] { new { row = 0, col = 1 } } }
        };

        // Act
        var response = await client.PostAsJsonAsync($"/api/games/{game.Id}/move", body);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert: opponent score is still zero
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(json.RootElement.TryGetProperty("opponentScore", out var opponentScore));
        Assert.Equal(0, opponentScore.GetInt32());
    }

    [Fact]
    public async Task Move_ThroughEmptyTile_YourScoreRemainsZero()
    {
        // Arrange: game in MovePhase with no coins on board
        var (game, tokenP1, _, p1PieceId) = await SeedGameInMovePhaseWithP1PieceAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        var body = new
        {
            pieceId = p1PieceId,
            segments = new[] { new[] { new { row = 0, col = 1 } } }
        };

        // Act
        var response = await client.PostAsJsonAsync($"/api/games/{game.Id}/move", body);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert: no coin collected, score stays 0
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(json.RootElement.TryGetProperty("yourScore", out var yourScore));
        Assert.Equal(0, yourScore.GetInt32());
    }

    // ── AC 9 : both players complete all moves → phase advances to CoinSpawn ──

    [Fact]
    public async Task Move_BothPlayersCompleteAllMoves_PhaseAdvancesToCoinSpawn()
    {
        // Arrange: P1 at (0,0), P2 at (7,7) — both in MovePhase
        var (game, tokenP1, tokenP2, p1PieceId, p2PieceId) =
            await SeedGameInMovePhaseWithBothPiecesAsync();

        var clientP1 = _factory.CreateClient();
        clientP1.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        var clientP2 = _factory.CreateClient();
        clientP2.DefaultRequestHeaders.Add("X-Bot-Token", tokenP2.ToString());

        // P1 moves first (P1 is always the first active player in MovePhase)
        await clientP1.PostAsJsonAsync($"/api/games/{game.Id}/move", new
        {
            pieceId = p1PieceId,
            segments = new[] { new[] { new { row = 0, col = 1 } } }
        });

        // Act: P2 moves — this completes all moves for both players
        var response = await clientP2.PostAsJsonAsync($"/api/games/{game.Id}/move", new
        {
            pieceId = p2PieceId,
            segments = new[] { new[] { new { row = 7, col = 6 } } }
        });
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert: turn rolled over → phase is now CoinSpawn (new turn)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(json.RootElement.TryGetProperty("phase", out var phaseElement));
        Assert.Equal("CoinSpawn", phaseElement.GetString());
    }

    [Fact]
    public async Task Move_AfterP1MovesButBeforeP2_PhaseIsStillMovePhase()
    {
        // Arrange
        var (game, tokenP1, _, p1PieceId, _) = await SeedGameInMovePhaseWithBothPiecesAsync();

        var clientP1 = _factory.CreateClient();
        clientP1.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act: only P1 moves — P2 still pending
        var response = await clientP1.PostAsJsonAsync($"/api/games/{game.Id}/move", new
        {
            pieceId = p1PieceId,
            segments = new[] { new[] { new { row = 0, col = 1 } } }
        });
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert: phase has NOT yet rolled over
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(json.RootElement.TryGetProperty("phase", out var phaseElement));
        Assert.Equal("MovePhase", phaseElement.GetString());
    }

    [Fact]
    public async Task Move_AfterP1Moves_ActivePlayerSwitchesToP2()
    {
        // Arrange: both players have one piece on board
        var (game, tokenP1, tokenP2, p1PieceId, _) = await SeedGameInMovePhaseWithBothPiecesAsync();

        var clientP1 = _factory.CreateClient();
        clientP1.DefaultRequestHeaders.Add("X-Bot-Token", tokenP1.ToString());

        // Act: P1 moves their piece
        var response = await clientP1.PostAsJsonAsync($"/api/games/{game.Id}/move", new
        {
            pieceId = p1PieceId,
            segments = new[] { new[] { new { row = 0, col = 1 } } }
        });
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert: activePlayer has switched to P2 (the other player)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(json.RootElement.TryGetProperty("activePlayer", out var activePlayerElement));
        Assert.NotEqual(JsonValueKind.Null, activePlayerElement.ValueKind);

        // The activePlayer ID should NOT be the same as the game's PlayerOne (P1)
        var activePlayerId = Guid.Parse(activePlayerElement.GetString()!);
        Assert.Equal(game.PlayerTwo, activePlayerId);
    }
}
