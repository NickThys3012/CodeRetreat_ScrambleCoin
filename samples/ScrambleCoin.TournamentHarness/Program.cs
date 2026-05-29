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
//    2. Registers <numBots> bots (self-registration, no admin key needed)
//    3. Starts the tournament
//    4. Runs all bots in parallel — each bot polls for its assigned game,
//       plays it, then polls again for knockout games, until done
// ─────────────────────────────────────────────────────────────────────────────

var baseUrl  = args.Length > 0 ? args[0]               : "http://localhost:5001";
var numBots  = args.Length > 1 && int.TryParse(args[1], out var n) ? n : 4;
const string adminKey    = "scramblecoin-admin";
const string tourneyName = "Harness Tournament";

// Deterministic bot IDs so reruns are reproducible
var bots = Enumerable.Range(1, numBots)
    .Select(i => (
        Id:   new Guid(i, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),  // Guid from seed
        Name: $"HarnessBot-{i}"
    ))
    .ToList();

var lineup = new[] { "Mickey", "Minnie", "Donald", "Goofy", "Scrooge" };

// ── 1. Create tournament ──────────────────────────────────────────────────────
using var adminHttp = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + '/') };
adminHttp.DefaultRequestHeaders.Add("X-Admin-Key", adminKey);

Print("🏆 Creating tournament…");
var createResp = await adminHttp.PostAsJsonAsync("api/tournament", new
{
    name            = tourneyName,
    maxParticipants = numBots,
    topN            = Math.Min(numBots, 4)
});

if (!createResp.IsSuccessStatusCode)
{
    var err = await createResp.Content.ReadAsStringAsync();
    Print($"❌ Failed to create tournament: {(int)createResp.StatusCode} {err}");
    return;
}

var createBody   = await createResp.Content.ReadFromJsonAsync<JsonElement>();
var tournamentId = createBody.GetProperty("tournamentId").GetGuid();
Print($"   Tournament ID: {tournamentId}");

// ── 2. Register bots ─────────────────────────────────────────────────────────
Print($"\n👥 Registering {numBots} bots…");

foreach (var (id, name) in bots)
{
    using var client = new BotClient(baseUrl);
    var ok = await client.JoinTournamentAsync(tournamentId, id, name, lineup);
    if (!ok)
    {
        Print($"❌ {name} failed to join. Aborting.");
        return;
    }
    Print($"   ✓ {name} joined");
}

// ── 3. Start tournament ───────────────────────────────────────────────────────
Print("\n🚀 Starting tournament…");
var startResp = await adminHttp.PostAsync(new Uri(adminHttp.BaseAddress!, $"api/tournament/{tournamentId}/start"), null);

if (!startResp.IsSuccessStatusCode)
{
    var err = await startResp.Content.ReadAsStringAsync();
    Print($"❌ Failed to start tournament: {(int)startResp.StatusCode} {err}");
    return;
}
Print("   Tournament started!");
Print($"\n🌐 Watch it live → {baseUrl}/tournament/{tournamentId}\n");
Print(new string('─', 60));

// ── 4. Run all bots in parallel ───────────────────────────────────────────────
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30)); // safety timeout

var botTasks = bots.Select(bot =>
    RunBotUntilDoneAsync(baseUrl, tournamentId, bot.Id, bot.Name, lineup, cts.Token)
).ToList();

await Task.WhenAll(botTasks);

Print(new string('─', 60));
Print("🎉 All bots finished. Check the tournament page for results!");

// ─────────────────────────────────────────────────────────────────────────────
//  Per-bot runner — polls for games, plays each one, handles knockout rounds
// ─────────────────────────────────────────────────────────────────────────────

static async Task RunBotUntilDoneAsync(
    string       baseUrl,
    Guid         tournamentId,
    Guid         botId,
    string       botName,
    string[]     lineup,
    CancellationToken ct)
{
    using var client   = new BotClient(baseUrl);
    var strategy       = new GreedyStrategy();
    var playedGames    = new HashSet<Guid>();
    var noGameStreak   = 0;
    const int maxNoGamePolls = 30;  // ~60s of no new games → assume done

    Print($"[{botName}] Starting…");

    while (!ct.IsCancellationRequested)
    {
        // Poll for assigned games
        var games = await client.GetBotGamesAsync(tournamentId, botId, ct);

        if (games is null)
        {
            // API error — back off and retry
            await Task.Delay(TimeSpan.FromSeconds(3), ct);
            continue;
        }

        var newGames = games.Where(g => !playedGames.Contains(g.GameId)).ToList();

        if (newGames.Count == 0)
        {
            noGameStreak++;
            if (noGameStreak >= maxNoGamePolls)
            {
                Print($"[{botName}] No new games for {maxNoGamePolls * 2}s — assuming tournament complete.");
                break;
            }
            // Log every 10 polls (~20s) so user knows bot is still alive
            if (noGameStreak % 10 == 1)
                Print($"[{botName}] ⏳ Waiting for next game… ({noGameStreak * 2}s elapsed)");
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
            continue;
        }

        noGameStreak = 0;

        foreach (var game in newGames)
        {
            playedGames.Add(game.GameId);
            Print($"[{botName}] 🎮 Playing {game.Stage} game — GameId: {game.GameId}");

            // Each game needs the correct token
            client.SetBotToken(game.Token);
            var loop = new GameLoop(client, strategy, $"{botName}");

            try
            {
                await loop.RunAsync(game.GameId, game.PlayerId, ct);
                Print($"[{botName}] ✅ Game {game.GameId} finished");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                Print($"[{botName}] ⚠️  Cancelled during game {game.GameId}");
                return;
            }
            catch (Exception ex)
            {
                Print($"[{botName}] ❌ Error in game {game.GameId}: {ex.Message}");
            }
        }

        // Wait before polling for next (knockout) game
        await Task.Delay(TimeSpan.FromSeconds(2), ct);
    }
}

// ─────────────────────────────────────────────────────────────────────────────

static void Print(string msg) =>
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
