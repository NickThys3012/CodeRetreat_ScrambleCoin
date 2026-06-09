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
using ScrambleCoin.Domain.Factories;
using ScrambleCoin.Domain.ValueObjects;
using ScrambleCoin.Infrastructure.Persistence;

namespace ScrambleCoin.Web.Tests;

/// <summary>
/// API-level tests for Issue #59 — verifies <c>GET /api/games/{id}/state</c> exposes
/// <c>availableFromTurn</c> per piece in camelCase, with the correct values from the
/// piece factory (Elsa = 2, Mickey = null, Merlin = 4).
/// </summary>
public class PieceTurnAvailabilityEndpointTests : IClassFixture<PieceTurnAvailabilityEndpointTests.TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public PieceTurnAvailabilityEndpointTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public sealed class TestWebApplicationFactory : WebApplicationFactory<Api.ApiMarker>
    {
        private readonly string _dbName = $"PieceTurnAvailabilityTestDb_{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                var descriptorsToRemove = services
                    .Where(d =>
                        d.ServiceType == typeof(DbContextOptions<ScrambleCoinDbContext>) ||
                        d.ServiceType == typeof(DbContextOptions) ||
                        d.ServiceType == typeof(ScrambleCoinDbContext))
                    .ToList();

                foreach (var d in descriptorsToRemove)
                    services.Remove(d);

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

    /// <summary>
    /// Seeds a started game whose player-one lineup mixes restricted and unrestricted
    /// pieces, plus a bot registration token for player one.
    /// </summary>
    private async Task<(Game game, Guid tokenP1)> SeedGameAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var gameRepo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var botRepo = scope.ServiceProvider.GetRequiredService<IBotRegistrationRepository>();

        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        string[] lineup1 = ["Mickey", "Elsa", "Scar", "Merlin", "Goofy"];
        string[] lineup2 = ["Mickey", "Minnie", "Donald", "Goofy", "Scrooge"];

        var pieces1 = lineup1.Select(n => PieceFactory.Create(n, p1)).ToList();
        var pieces2 = lineup2.Select(n => PieceFactory.Create(n, p2)).ToList();

        var game = new Game(p1, p2, new Board());
        game.SetLineup(p1, new Lineup(pieces1));
        game.SetLineup(p2, new Lineup(pieces2));
        game.Start();

        await gameRepo.SaveAsync(game);

        var tokenP1 = Guid.NewGuid();
        await botRepo.SaveAsync(new BotRegistration(tokenP1, p1, game.Id));
        await botRepo.SaveAsync(new BotRegistration(Guid.NewGuid(), p2, game.Id));

        return (game, tokenP1);
    }

    private async Task<JsonElement> FetchStateAsync()
    {
        var (game, token) = await SeedGameAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Bot-Token", token.ToString());

        var response = await client.GetAsync(new Uri($"/api/games/{game.Id}/state", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }

    [Fact]
    public async Task GetBoardState_EachPiece_ContainsAvailableFromTurnField()
    {
        var root = await FetchStateAsync();

        foreach (var piece in root.GetProperty("yourPieces").EnumerateArray())
        {
            Assert.True(piece.TryGetProperty("availableFromTurn", out _),
                "Each piece in 'yourPieces' must include an 'availableFromTurn' field (camelCase).");
        }
    }

    [Fact]
    public async Task GetBoardState_RestrictedPiece_HasCorrectAvailableFromTurn()
    {
        var root = await FetchStateAsync();

        var elsa = root.GetProperty("yourPieces").EnumerateArray()
            .Single(p => p.GetProperty("name").GetString() == "Elsa");

        Assert.Equal(2, elsa.GetProperty("availableFromTurn").GetInt32());
    }

    [Fact]
    public async Task GetBoardState_UnrestrictedPiece_HasNullAvailableFromTurn()
    {
        var root = await FetchStateAsync();

        var mickey = root.GetProperty("yourPieces").EnumerateArray()
            .Single(p => p.GetProperty("name").GetString() == "Mickey");

        Assert.Equal(JsonValueKind.Null, mickey.GetProperty("availableFromTurn").ValueKind);
    }

    [Fact]
    public async Task GetBoardState_HighRestrictionPiece_HasCorrectAvailableFromTurn()
    {
        var root = await FetchStateAsync();

        var merlin = root.GetProperty("yourPieces").EnumerateArray()
            .Single(p => p.GetProperty("name").GetString() == "Merlin");

        Assert.Equal(4, merlin.GetProperty("availableFromTurn").GetInt32());
    }
}
