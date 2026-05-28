using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScrambleCoin.Infrastructure.Persistence;

namespace ScrambleCoin.Web.Tests;

/// <summary>
/// Integration tests for the Tournament REST API endpoints (Issue #52).
/// Uses <see cref="WebApplicationFactory{TEntryPoint}"/> with an EF Core in-memory database.
/// </summary>
public class TournamentEndpointTests : IClassFixture<TournamentEndpointTests.TestWebApplicationFactory>
{
    private const string AdminKey = "scramblecoin-admin";

    private static readonly string[] DefaultLineup =
        ["Mickey", "Minnie", "Donald", "Goofy", "Scrooge"];

    private readonly TestWebApplicationFactory _factory;

    public TournamentEndpointTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── Test factory ──────────────────────────────────────────────────────────

    public sealed class TestWebApplicationFactory : WebApplicationFactory<Api.ApiMarker>
    {
        private readonly string _dbName = $"TournamentTestDb_{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                // Replace the real SQL Server DbContext with an in-memory one.
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private HttpClient CreateAdminClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Admin-Key", AdminKey);
        return client;
    }

    /// <summary>Creates a tournament via the API and returns its ID.</summary>
    private async Task<Guid> CreateTournamentAsync(
        HttpClient client,
        string name = "Test Cup",
        int maxParticipants = 8,
        int topN = 2)
    {
        var body = new { Name = name, MaxParticipants = maxParticipants, TopN = topN };
        var response = await client.PostAsJsonAsync("/api/tournament", body);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("tournamentId").GetGuid();
    }

    /// <summary>Registers a bot for a tournament via the API.</summary>
    private async Task AddParticipantAsync(HttpClient client, Guid tournamentId, Guid botId, string botName)
    {
        var body = new { BotId = botId, BotName = botName, Lineup = DefaultLineup };
        var response = await client.PostAsJsonAsync($"/api/tournament/{tournamentId}/participants", body);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Posts to a no-body endpoint using a <see cref="Uri"/> to satisfy CA2234.</summary>
    private static Task<HttpResponseMessage> PostEmptyAsync(HttpClient client, string relativeUrl) =>
        client.PostAsync(new Uri(relativeUrl, UriKind.Relative), content: null);

    /// <summary>Gets a relative URL using a <see cref="Uri"/> to satisfy CA2234.</summary>
    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string relativeUrl) =>
        client.GetAsync(new Uri(relativeUrl, UriKind.Relative));

    // ══════════════════════════════════════════════════════════════════════════
    // POST /api/tournament
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PostTournament_WithValidAdminKey_Returns201Created()
    {
        var client = CreateAdminClient();
        var body = new { Name = "Test Cup", MaxParticipants = 8, TopN = 4 };

        var response = await client.PostAsJsonAsync("/api/tournament", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task PostTournament_ResponseBody_ContainsTournamentId()
    {
        var client = CreateAdminClient();
        var body = new { Name = "Spring Cup", MaxParticipants = 8, TopN = 4 };

        var response = await client.PostAsJsonAsync("/api/tournament", body);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("tournamentId", out var idProp));
        Assert.NotEqual(Guid.Empty, idProp.GetGuid());
    }

    [Fact]
    public async Task PostTournament_WithoutAdminKey_Returns401()
    {
        var client = _factory.CreateClient(); // no admin key
        var body = new { Name = "Test Cup", MaxParticipants = 8 };

        var response = await client.PostAsJsonAsync("/api/tournament", body);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostTournament_WithInvalidName_Returns400()
    {
        var client = CreateAdminClient();
        // Empty name violates the domain invariant
        var body = new { Name = "", MaxParticipants = 8, TopN = 4 };

        var response = await client.PostAsJsonAsync("/api/tournament", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // POST /api/tournament/{id}/participants
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PostParticipants_KnownTournament_Returns204NoContent()
    {
        var client = CreateAdminClient();
        var tournamentId = await CreateTournamentAsync(client);

        var body = new { BotId = Guid.NewGuid(), BotName = "Bot1", Lineup = DefaultLineup };
        var response = await client.PostAsJsonAsync($"/api/tournament/{tournamentId}/participants", body);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task PostParticipants_UnknownTournament_Returns404()
    {
        var client = CreateAdminClient();
        var unknownId = Guid.NewGuid();
        var body = new { BotId = Guid.NewGuid(), BotName = "Bot1", Lineup = DefaultLineup };

        var response = await client.PostAsJsonAsync($"/api/tournament/{unknownId}/participants", body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostParticipants_WithoutAdminKey_Returns401()
    {
        var adminClient = CreateAdminClient();
        var tournamentId = await CreateTournamentAsync(adminClient);

        var publicClient = _factory.CreateClient(); // no admin key
        var body = new { BotId = Guid.NewGuid(), BotName = "Bot1", Lineup = DefaultLineup };

        var response = await publicClient.PostAsJsonAsync($"/api/tournament/{tournamentId}/participants", body);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostParticipants_DuplicateBot_Returns400()
    {
        var client = CreateAdminClient();
        var tournamentId = await CreateTournamentAsync(client);
        var botId = Guid.NewGuid();

        // Register once → should succeed
        var body = new { BotId = botId, BotName = "Bot1", Lineup = DefaultLineup };
        await client.PostAsJsonAsync($"/api/tournament/{tournamentId}/participants", body);

        // Register same bot again → 400
        var response = await client.PostAsJsonAsync($"/api/tournament/{tournamentId}/participants", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostParticipants_AfterTournamentStarted_Returns409()
    {
        var client = CreateAdminClient();
        var tournamentId = await CreateTournamentAsync(client);
        await AddParticipantAsync(client, tournamentId, Guid.NewGuid(), "Bot1");
        await AddParticipantAsync(client, tournamentId, Guid.NewGuid(), "Bot2");
        await PostEmptyAsync(client, $"/api/tournament/{tournamentId}/start");

        // Try to add a participant to an already-started tournament → 409
        var body = new { BotId = Guid.NewGuid(), BotName = "LateBot", Lineup = DefaultLineup };
        var response = await client.PostAsJsonAsync($"/api/tournament/{tournamentId}/participants", body);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // POST /api/tournament/{id}/start
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PostStart_WithTwoParticipants_Returns200OK()
    {
        var client = CreateAdminClient();
        var tournamentId = await CreateTournamentAsync(client);
        await AddParticipantAsync(client, tournamentId, Guid.NewGuid(), "Bot1");
        await AddParticipantAsync(client, tournamentId, Guid.NewGuid(), "Bot2");

        var response = await PostEmptyAsync(client, $"/api/tournament/{tournamentId}/start");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostStart_AlreadyStarted_Returns409Conflict()
    {
        var client = CreateAdminClient();
        var tournamentId = await CreateTournamentAsync(client);
        await AddParticipantAsync(client, tournamentId, Guid.NewGuid(), "Bot1");
        await AddParticipantAsync(client, tournamentId, Guid.NewGuid(), "Bot2");

        // Start once
        await PostEmptyAsync(client, $"/api/tournament/{tournamentId}/start");

        // Try to start again → 409
        var response = await PostEmptyAsync(client, $"/api/tournament/{tournamentId}/start");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PostStart_UnknownTournament_Returns404()
    {
        var client = CreateAdminClient();
        var unknownId = Guid.NewGuid();

        var response = await PostEmptyAsync(client, $"/api/tournament/{unknownId}/start");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostStart_WithoutAdminKey_Returns401()
    {
        var adminClient = CreateAdminClient();
        var tournamentId = await CreateTournamentAsync(adminClient);

        var publicClient = _factory.CreateClient();
        var response = await PostEmptyAsync(publicClient, $"/api/tournament/{tournamentId}/start");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // GET /api/tournament/{id}/standings
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetStandings_StartedTournament_Returns200OK()
    {
        var client = CreateAdminClient();
        var tournamentId = await CreateTournamentAsync(client);
        await AddParticipantAsync(client, tournamentId, Guid.NewGuid(), "Bot1");
        await AddParticipantAsync(client, tournamentId, Guid.NewGuid(), "Bot2");
        await PostEmptyAsync(client, $"/api/tournament/{tournamentId}/start");

        var response = await GetAsync(client, $"/api/tournament/{tournamentId}/standings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetStandings_ResponseBody_ContainsTwoStandingEntries()
    {
        var client = CreateAdminClient();
        var tournamentId = await CreateTournamentAsync(client);
        await AddParticipantAsync(client, tournamentId, Guid.NewGuid(), "Bot1");
        await AddParticipantAsync(client, tournamentId, Guid.NewGuid(), "Bot2");
        await PostEmptyAsync(client, $"/api/tournament/{tournamentId}/start");

        var response = await GetAsync(client, $"/api/tournament/{tournamentId}/standings");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var standings = doc.RootElement.GetProperty("standings");
        Assert.Equal(2, standings.GetArrayLength());
    }

    [Fact]
    public async Task GetStandings_UnknownTournament_Returns404()
    {
        var client = CreateAdminClient();
        var unknownId = Guid.NewGuid();

        var response = await GetAsync(client, $"/api/tournament/{unknownId}/standings");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // GET /api/tournament/{id}/bracket
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetBracket_StartedTournament_Returns200OK()
    {
        var client = CreateAdminClient();
        var tournamentId = await CreateTournamentAsync(client);
        await AddParticipantAsync(client, tournamentId, Guid.NewGuid(), "Bot1");
        await AddParticipantAsync(client, tournamentId, Guid.NewGuid(), "Bot2");
        await PostEmptyAsync(client, $"/api/tournament/{tournamentId}/start");

        var response = await GetAsync(client, $"/api/tournament/{tournamentId}/bracket");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetBracket_ResponseBody_ContainsTournamentId()
    {
        var client = CreateAdminClient();
        var tournamentId = await CreateTournamentAsync(client, "Bracket Cup");
        await AddParticipantAsync(client, tournamentId, Guid.NewGuid(), "Bot1");
        await AddParticipantAsync(client, tournamentId, Guid.NewGuid(), "Bot2");
        await PostEmptyAsync(client, $"/api/tournament/{tournamentId}/start");

        var response = await GetAsync(client, $"/api/tournament/{tournamentId}/bracket");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("tournamentId", out var idProp));
        Assert.Equal(tournamentId, idProp.GetGuid());
    }

    [Fact]
    public async Task GetBracket_StartedTournament_HasGroupMatches()
    {
        var client = CreateAdminClient();
        var tournamentId = await CreateTournamentAsync(client);
        await AddParticipantAsync(client, tournamentId, Guid.NewGuid(), "Bot1");
        await AddParticipantAsync(client, tournamentId, Guid.NewGuid(), "Bot2");
        await PostEmptyAsync(client, $"/api/tournament/{tournamentId}/start");

        var response = await GetAsync(client, $"/api/tournament/{tournamentId}/bracket");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var groupMatches = doc.RootElement.GetProperty("groupMatches");
        Assert.True(groupMatches.GetArrayLength() > 0, "Expected at least one group match in the bracket.");
    }

    [Fact]
    public async Task GetBracket_UnknownTournament_Returns404()
    {
        var client = CreateAdminClient();
        var unknownId = Guid.NewGuid();

        var response = await GetAsync(client, $"/api/tournament/{unknownId}/bracket");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // POST /api/tournament/{id}/cancel
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PostCancel_PendingTournament_Returns200OK()
    {
        var client = CreateAdminClient();
        var tournamentId = await CreateTournamentAsync(client);

        var response = await PostEmptyAsync(client, $"/api/tournament/{tournamentId}/cancel");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostCancel_StartedTournament_Returns200OK()
    {
        var client = CreateAdminClient();
        var tournamentId = await CreateTournamentAsync(client);
        await AddParticipantAsync(client, tournamentId, Guid.NewGuid(), "Bot1");
        await AddParticipantAsync(client, tournamentId, Guid.NewGuid(), "Bot2");
        await PostEmptyAsync(client, $"/api/tournament/{tournamentId}/start");

        var response = await PostEmptyAsync(client, $"/api/tournament/{tournamentId}/cancel");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostCancel_UnknownTournament_Returns404()
    {
        var client = CreateAdminClient();
        var unknownId = Guid.NewGuid();

        var response = await PostEmptyAsync(client, $"/api/tournament/{unknownId}/cancel");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostCancel_WithoutAdminKey_Returns401()
    {
        var adminClient = CreateAdminClient();
        var tournamentId = await CreateTournamentAsync(adminClient);

        var publicClient = _factory.CreateClient();
        var response = await PostEmptyAsync(publicClient, $"/api/tournament/{tournamentId}/cancel");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostCancel_AlreadyCancelledTournament_Returns409()
    {
        var client = CreateAdminClient();
        var tournamentId = await CreateTournamentAsync(client);

        // Cancel once → should succeed
        await PostEmptyAsync(client, $"/api/tournament/{tournamentId}/cancel");

        // Cancel again → 409 Conflict (TournamentInvalidStateException)
        var response = await PostEmptyAsync(client, $"/api/tournament/{tournamentId}/cancel");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
