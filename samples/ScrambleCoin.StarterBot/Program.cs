using ScrambleCoin.StarterBot;

// ── Configuration ─────────────────────────────────────────────────────────────

var baseUrl  = Environment.GetEnvironmentVariable("BASE_URL")  ?? "http://localhost:5000";
var botName  = Environment.GetEnvironmentVariable("BOT_NAME")  ?? "StarterBot";
var gameIdEnv = Environment.GetEnvironmentVariable("GAME_ID");

// Default lineup — 5 starter pieces. Change these to use different pieces.
// All available starter piece names: Mickey, Minnie, Donald, Goofy, Scrooge
var lineup = new[] { "Mickey", "Minnie", "Donald", "Goofy", "Scrooge" };

Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
Console.WriteLine($"║  ScrambleCoin StarterBot — {botName,-32}║");
Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
Console.WriteLine($"Server:  {baseUrl}");
Console.WriteLine($"Lineup:  {string.Join(", ", lineup)}");
Console.WriteLine();

// ── Graceful shutdown ─────────────────────────────────────────────────────────

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("\nShutting down…");
    cts.Cancel();
};

// ── Bot setup ─────────────────────────────────────────────────────────────────

var client   = new BotClient(baseUrl);
using var clientDisposal = client; // ensure HttpClient is disposed on exit
var strategy = new GreedyStrategy();        // ← swap in your own IStrategy here
var loop     = new GameLoop(client, strategy, botName);

// ── Join a game ───────────────────────────────────────────────────────────────

Guid gameId;
Guid playerId;
Guid token;

if (!string.IsNullOrWhiteSpace(gameIdEnv) && Guid.TryParse(gameIdEnv, out var directGameId))
{
    // Direct join — admin pre-created a game and shared its ID
    Console.WriteLine($"Joining game {directGameId} directly…");
    var join = await client.JoinGameAsync(directGameId, lineup, cts.Token);

    if (join is null)
    {
        Console.WriteLine("Failed to join game. Exiting.");
        return 1;
    }

    gameId   = directGameId;
    playerId = join.PlayerId;
    token    = join.Token;
    Console.WriteLine($"Joined! PlayerId: {playerId}");
}
else
{
    // Matchmaking — bot enqueues and waits for an opponent
    var match = await loop.JoinViaQueueAsync(lineup, cts.Token);

    if (match is null)
    {
        Console.WriteLine("Failed to join matchmaking queue. Exiting.");
        return 1;
    }

    (gameId, playerId, token) = match.Value;
}

// Authenticate all subsequent requests with the assigned bot token
client.SetBotToken(token);
Console.WriteLine($"Bot token set. Ready to play.");
Console.WriteLine();

// ── Play the game ─────────────────────────────────────────────────────────────

try
{
    await loop.RunAsync(gameId, playerId, cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Bot stopped by user.");
}
catch (Exception ex)
{
    Console.WriteLine($"[FATAL] Unexpected error: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    return 2;
}

return 0;
