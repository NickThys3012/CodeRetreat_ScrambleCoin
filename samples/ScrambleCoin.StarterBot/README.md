# ScrambleCoin StarterBot — C# Example Bot

A self-contained C# console application that plays a complete ScrambleCoin game.
Copy this project, run it with a single command, and replace the strategy logic with your own.

---

## Quick Start

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- A running ScrambleCoin API server

### 1. Configure environment variables

```bash
cp .env.example .env
# Edit .env and set BASE_URL to point at your server
```

| Variable  | Required | Description                                                          |
|-----------|----------|----------------------------------------------------------------------|
| `BASE_URL` | Yes     | Base URL of the ScrambleCoin server, e.g. `http://localhost:5000`    |
| `BOT_NAME` | No      | Display name for this bot (console output only). Default: `StarterBot` |
| `GAME_ID`  | No      | Join a specific pre-created game. Leave blank to use matchmaking.    |

### 2. Run the bot

**With `.env` file (Linux/macOS):**
```bash
export $(cat .env | xargs) && dotnet run
```

**Inline (Linux/macOS):**
```bash
BASE_URL=http://localhost:5000 BOT_NAME=MyBot dotnet run
```

**Windows PowerShell:**
```powershell
$env:BASE_URL = "http://localhost:5000"
$env:BOT_NAME = "MyBot"
dotnet run
```

**Join a specific game (admin pre-created):**
```bash
BASE_URL=http://localhost:5000 GAME_ID=<uuid> dotnet run
```

---

## How It Works

### Game flow

```
1. Enqueue for matchmaking (or join a specific game via GAME_ID)
2. Wait to be matched with an opponent
3. Set X-Bot-Token from the join response
4. Poll GET /api/games/{gameId}/state every second
5. For each PlacePhase:   place an unplaced piece on the first free border tile
6. For each MovePhase:    move each placed piece one step toward the nearest coin
7. When the game ends:    print the final score
```

### Phases

| Phase        | What the bot does                                          |
|--------------|------------------------------------------------------------|
| `CoinSpawn`  | Wait — the server places coins automatically               |
| `PlacePhase` | Submit `POST /api/games/{id}/place` for each unplaced piece |
| `MovePhase`  | Submit `POST /api/games/{id}/move` **only when it is your turn** (`ActivePlayer == yourPlayerId`) |
| `null`       | Game hasn't started yet (Turn = 0) or game has ended (Turn > 0) |

---

## Customising the Strategy

The bot's decision-making is isolated in two files:

- **`IStrategy.cs`** — the interface you implement
- **`GreedyStrategy.cs`** — the default implementation (one step toward nearest coin)

### To write your own strategy

1. Create a new class that implements `IStrategy`:

```csharp
public sealed class MyStrategy : IStrategy
{
    public PlacementDecision DecidePlacement(BoardState state, PieceState piece)
    {
        // Choose where to place this piece, or skip
        var firstFreeBorderTile = FindMyFavouriteTile(state);
        return firstFreeBorderTile is not null
            ? new PlacementDecision.Place(piece.PieceId, firstFreeBorderTile)
            : new PlacementDecision.Skip();
    }

    public MoveDecision DecideMove(BoardState state, PieceState piece)
    {
        // For a piece with MovesPerTurn = 1, return one segment with one step
        var target = PickBestTarget(state, piece);
        var segments = new[] { new[] { target } };
        return new MoveDecision(piece.PieceId, segments);
    }
}
```

2. In `Program.cs`, swap the strategy:

```csharp
// Before:
var strategy = new GreedyStrategy();

// After:
var strategy = new MyStrategy();
```

That's it — the plumbing (`BotClient`, `GameLoop`) stays unchanged.

---

## Project Structure

```
ScrambleCoin.StarterBot/
├── Program.cs          — entry point; reads env vars, joins game, starts loop
├── BotClient.cs        — HttpClient wrapper for all API calls
├── IStrategy.cs        — strategy interface
├── GreedyStrategy.cs   — default strategy (greedy coin-chasing)
├── GameLoop.cs         — poll → decide → submit loop
├── Models/
│   ├── ApiModels.cs    — request/response DTOs
│   └── Decisions.cs    — PlacementDecision, MoveDecision
├── .env.example        — environment variable template
└── README.md           — this file
```

---

## Full API Contract

All endpoints are on the ScrambleCoin API server (`BASE_URL`).

### Authentication

After joining a game, every request must include:

```
X-Bot-Token: <token-guid>
```

The token is returned by the join/queue endpoints.

---

### Matchmaking (recommended for 1v1)

#### Enqueue for matchmaking
```
POST /api/games/queue
Content-Type: application/json

{
  "lineup": ["Mickey", "Minnie", "Donald", "Goofy", "Scrooge"]
}
```

**Responses:**
- `200 OK` — matched immediately: `{ "status": "matched", "gameId": "...", "playerId": "...", "token": "..." }`
- `202 Accepted` — waiting: `{ "queueId": "..." }` — poll the queue
- `400 Bad Request` — invalid lineup
- `409 Conflict` — bot already queued or in an active game

#### Poll matchmaking queue
```
GET /api/games/queue/{queueId}
```

**Responses:**
- `200 OK` — `{ "status": "waiting" }` or `{ "status": "matched", "gameId": "...", "playerId": "...", "token": "..." }`
- `409 Conflict` — queue entry timed out (re-enqueue)

---

### Direct join (admin pre-created game)

```
POST /api/games/{gameId}/join
Content-Type: application/json

{
  "lineup": ["Mickey", "Minnie", "Donald", "Goofy", "Scrooge"]
}
```

**Response `201 Created`:**
```json
{ "playerId": "...", "token": "..." }
```

**Error responses:**
- `409 Conflict` — game is already full
- `404 Not Found` — game not found

---

### Get board state

```
GET /api/games/{gameId}/state
X-Bot-Token: <token>
```

**Response `200 OK`:**
```json
{
  "turn": 1,
  "phase": "PlacePhase",
  "yourScore": 0,
  "opponentScore": 0,
  "board": { "tiles": [ ... ] },
  "yourPieces": [ ... ],
  "opponentPieces": [ ... ],
  "availableCoins": [ ... ],
  "activePlayer": null
}
```

| Field          | Type     | Description                                                          |
|----------------|----------|----------------------------------------------------------------------|
| `turn`         | int      | Current turn number (1–5); 0 before game starts                      |
| `phase`        | string?  | `"CoinSpawn"`, `"PlacePhase"`, `"MovePhase"`, or `null`              |
| `activePlayer` | string?  | PlayerId of the bot whose turn it is (MovePhase only), or `null`     |
| `yourPieces`   | array    | Your pieces with positions, movement type, and on-board status       |
| `availableCoins` | array  | All coins on the board with position and value                       |

---

### Submit placement

```
POST /api/games/{gameId}/place
X-Bot-Token: <token>
Content-Type: application/json

{
  "action": "place",
  "pieceId": "...",
  "position": { "row": 0, "col": 3 }
}
```

**Actions:**
| Action    | Required fields           | Description                        |
|-----------|---------------------------|------------------------------------|
| `"place"` | `pieceId`, `position`     | Place a piece on the board         |
| `"replace"` | `pieceId`, `replacedPieceId`, `position` | Remove one piece and place another |
| `"skip"`  | _(none)_                  | Skip placement for this turn       |

**Response `200 OK`:**
```json
{ "phase": "MovePhase", "activePlayer": "..." }
```

**Error responses:**
- `400 Bad Request` — invalid action, bad position, wrong phase
- `403 Forbidden` — wrong/missing token
- `409 Conflict` — already acted this turn

---

### Submit move

```
POST /api/games/{gameId}/move
X-Bot-Token: <token>
Content-Type: application/json

{
  "pieceId": "...",
  "segments": [
    [ { "row": 1, "col": 3 } ]
  ]
}
```

`segments` must contain exactly `movesPerTurn` entries.
Each segment is a list of positions the piece steps through during that move action
(not including the starting position). An empty segment `[]` means the piece stays still
for that action.

**Example — piece with `movesPerTurn: 2`:**
```json
{
  "pieceId": "...",
  "segments": [
    [ { "row": 2, "col": 3 } ],
    [ { "row": 2, "col": 4 } ]
  ]
}
```

**Response `200 OK`:**
```json
{ "phase": "MovePhase", "activePlayer": "...", "yourScore": 3, "opponentScore": 0 }
```

**Error responses:**
- `400 Bad Request` — invalid move (wrong phase, illegal path, wrong number of segments)
- `403 Forbidden` — wrong/missing token
- `404 Not Found` — game not found

---

### Get game result

```
GET /api/games/{gameId}/result
X-Bot-Token: <token>
```

**Response `200 OK`** (game finished):
```json
{
  "gameId": "...",
  "status": "finished",
  "playerOneId": "...",
  "playerOneScore": 7,
  "playerTwoId": "...",
  "playerTwoScore": 4,
  "winnerId": "...",
  "isDraw": false
}
```

**Error responses:**
- `409 Conflict` — game not finished yet (retry later)

---

## Piece Catalogue

| Name     | Entry Point | Movement     | Max Distance | Moves/Turn |
|----------|-------------|--------------|:------------:|:----------:|
| Mickey   | Borders     | Orthogonal   | 3            | 1          |
| Minnie   | Borders     | Diagonal     | 3            | 1          |
| Donald   | Corners     | AnyDirection | 3            | 1          |
| Goofy    | Corners     | Jump         | 3            | 1          |
| Scrooge  | Corners     | AnyDirection | 2            | 1          |
| Elsa     | Borders     | Orthogonal   | 4            | 1          |
| Cogsworth| Borders     | AnyDirection | 1+2          | 2          |
| Lumiere  | Borders     | AnyDirection | 1+2          | 2          |
| …        | …           | …            | …            | …          |

> **Starter lineup (recommended for beginners):** `["Mickey", "Minnie", "Donald", "Goofy", "Scrooge"]`

**Entry point types:**
- `Borders` — any non-corner border tile (row 0, row 7, col 0, or col 7)
- `Corners` — one of the 4 corner tiles: (0,0), (0,7), (7,0), (7,7)
- `Anywhere` — any free tile on the board

---

## Error handling

The `BotClient` prints clear messages for all API errors and returns `null` on recoverable errors.
The `GameLoop` catches `HttpRequestException` (connection lost) and retries after 3 seconds.

Common errors:

| Code | When it happens                        | What to do                                  |
|------|----------------------------------------|---------------------------------------------|
| 400  | Invalid move or placement              | Check your strategy's position calculations |
| 403  | Wrong or missing `X-Bot-Token`         | Ensure `client.SetBotToken(token)` was called |
| 409  | Already acted this turn / game full    | The loop handles this — no action needed    |
| 404  | Wrong game ID                          | Check `GAME_ID` env var or queue response   |
