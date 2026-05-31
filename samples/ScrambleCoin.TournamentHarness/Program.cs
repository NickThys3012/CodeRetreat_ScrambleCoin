using System.Net.Http.Json;
using System.Text.Json;
using ScrambleCoin.StarterBot;
using ScrambleCoin.StarterBot.Models;

// ─────────────────────────────────────────────────────────────────────────────
//  ScrambleCoin Tournament Harness
//
//  Usage: dotnet run -- [baseUrl] [numBots]
//  Defaults: http://localhost:5001  4
//
//  What it does:
//    1. Creates a tournament via the admin API
//    2. Registers <numBots> bots (self-registration)
//    3. Starts the tournament
//    4. Each bot monitors for assigned games via polling and spins up a
//       dedicated GameLoop for EACH game as soon as it appears — games run
//       concurrently so a slow opponent in one game never blocks a bot from
//       playing another.
// ─────────────────────────────────────────────────────────────────────────────

var baseUrl      = args.Length > 0 ? args[0]                                : "http://localhost:5001";
var numBots      = args.Length > 1 && int.TryParse(args[1], out var n) ? n : 4;
const string adminKey    = "scramblecoin-admin";
const string tourneyName = "Harness Tournament";

// Deterministic bot IDs so reruns are reproducible
var bots = Enumerable.Range(1, numBots)
    .Select(i => (
        Id:   new Guid(i, 0, 0, new byte[8]),
        Name: $"HarnessBot-{i}"
    ))
    .ToList();

var lineup = new[] { "Mickey", "Minnie", "Donald", "Goofy", "Scrooge" };

// ── 1. Create tournament ──────────────────────────────────────────────────────
using var adminHttp = new HttpClient();
adminHttp.BaseAddress = new Uri(baseUrl.TrimEnd('/') + '/');
adminHttp.DefaultRequestHeaders.Add("X-Admin-Key", adminKey);

Log("🏆 Creating tournament…");
var createResp = await adminHttp.PostAsJsonAsync("api/tournament", new
{
    name            = tourneyName,
    maxParticipants = numBots,
    topN            = numBots
});

if (!createResp.IsSuccessStatusCode)
{
    Log($"❌ Failed to create tournament: {(int)createResp.StatusCode} {await createResp.Content.ReadAsStringAsync()}");
    return;
}

var createBody   = await createResp.Content.ReadFromJsonAsync<JsonElement>();
var tournamentId = createBody.GetProperty("tournamentId").GetGuid();
Log($"   Tournament ID: {tournamentId}");

// ── 2. Register bots ─────────────────────────────────────────────────────────
Log($"\n👥 Registering {numBots} bots…");

foreach (var bot in bots)
{
    using var client = new BotClient(baseUrl);
    if (!await client.JoinTournamentAsync(tournamentId, bot.Id, bot.Name, lineup))
    {
        Log($"❌ {bot.Name} failed to join. Aborting.");
        return;
    }
    Log($"   ✓ {bot.Name} joined");
}

// ── 3. Start tournament ───────────────────────────────────────────────────────
Log("\n🚀 Starting tournament…");
var startResp = await adminHttp.PostAsync(
    new Uri(adminHttp.BaseAddress!, $"api/tournament/{tournamentId}/start"), null);

if (!startResp.IsSuccessStatusCode)
{
    Log($"❌ Failed to start tournament: {(int)startResp.StatusCode} {await startResp.Content.ReadAsStringAsync()}");
    return;
}

Log("   Tournament started!");
Log($"\n🌐 Watch it live → {baseUrl}/tournament/{tournamentId}\n");
Log(new string('─', 60));

// ── 4. Run all bots in parallel ───────────────────────────────────────────────
// Global safety timeout — no tournament should run longer than 30 minutes.
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));

await Task.WhenAll(bots.Select(bot =>
    RunBotAsync(baseUrl, tournamentId, bot.Id, bot.Name, cts.Token)));

Log(new string('─', 60));
Log("🎉 All bots finished. Check the tournament page for results!");
return;

// ─────────────────────────────────────────────────────────────────────────────
//  Per-bot runner
//
//  Polls for game assignments every 2 seconds and immediately starts a
//  concurrent GameLoop for each new game.  This means a bot can play two
//  games simultaneously — which happens naturally in round-robin tournaments
//  where the scheduler assigns the next round before a slow game from the
//  current round ends.
// ─────────────────────────────────────────────────────────────────────────────

static async Task RunBotAsync(
    string            baseUrl,
    Guid              tournamentId,
    Guid              botId,
    string            botName,
    CancellationToken ct)
{
    using var discoveryClient = new BotClient(baseUrl);

    // gameId → running task (never double-start the same game)
    var activeGames    = new Dictionary<Guid, Task>();
    var completedGames = new HashSet<Guid>(); // never restart a finished game
    var noNewStreak  = 0;
    const int maxNoNew = 15; // 15 × 2 s = 30 s with no new games + no active games → done

    Log($"[{botName}] Ready — watching for game assignments…");

    while (!ct.IsCancellationRequested)
    {
        // Remove completed game tasks and log any faults.
        foreach (var id in activeGames.Keys
                     .Where(id => activeGames[id].IsCompleted)
                     .ToList())
        {
            if (activeGames[id].IsFaulted)
                Log($"[{botName}] ⚠  Game {id} faulted: {activeGames[id].Exception?.GetBaseException().Message}");
            completedGames.Add(id);
            activeGames.Remove(id);
        }

        var games = await discoveryClient.GetBotGamesAsync(tournamentId, botId, ct);
        if (games is null)
        {
            await Task.Delay(TimeSpan.FromSeconds(3), ct);
            continue;
        }

        var newGames = games
            .Where(g => !activeGames.ContainsKey(g.GameId) && !completedGames.Contains(g.GameId))
            .ToList();

        if (newGames.Count == 0)
        {
            // Exit only when there are no active games and no new ones for a while.
            if (activeGames.Count == 0 && ++noNewStreak >= maxNoNew)
            {
                Log($"[{botName}] No new or active games for ~{maxNoNew * 2} s — done.");
                break;
            }
        }
        else
        {
            noNewStreak = 0;

            foreach (var game in newGames)
            {
                Log($"[{botName}] 🎮 {game.Stage} game assigned — {game.GameId}");
                var g = game; // capture for lambda
                activeGames[g.GameId] = Task.Run(() => PlayGameAsync(baseUrl, botName, g, ct), ct);
            }
        }

        await Task.Delay(TimeSpan.FromSeconds(2), ct);
    }

    // Wait for any in-flight games before returning.
    if (activeGames.Count > 0)
    {
        Log($"[{botName}] ⏳ Waiting for {activeGames.Count} in-flight game(s)…");
        try { await Task.WhenAll(activeGames.Values).WaitAsync(TimeSpan.FromMinutes(10), ct); }
        catch (OperationCanceledException) { /* shutdown */ }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  Plays a single game end-to-end.
//  Each invocation gets its own BotClient so token and SignalR state are
//  fully isolated — concurrent games for the same bot don't interfere.
// ─────────────────────────────────────────────────────────────────────────────

static async Task PlayGameAsync(
    string            baseUrl,
    string            botName,
    BotGameInfo       game,
    CancellationToken ct)
{
    using var gameCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    gameCts.CancelAfter(TimeSpan.FromMinutes(10)); // per-game safety timeout

    using var gameClient = new BotClient(baseUrl);
    gameClient.SetBotToken(game.Token);

    var loop = new GameLoop(gameClient, new GreedyStrategy(), botName);

    try
    {
        await loop.RunAsync(game.GameId, game.PlayerId, gameCts.Token);
        Log($"[{botName}] ✅ {game.Stage} game {game.GameId} complete");
    }
    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
    {
        Log($"[{botName}] ⏱  Game {game.GameId} timed out after 10 min — skipping.");
    }
    catch (OperationCanceledException)
    {
        // Global cancellation — swallow, parent will log.
    }
    catch (Exception ex)
    {
        Log($"[{botName}] ❌ Game {game.GameId}: {ex.Message}");
    }
}

// ─────────────────────────────────────────────────────────────────────────────

static void Log(string msg) =>
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
