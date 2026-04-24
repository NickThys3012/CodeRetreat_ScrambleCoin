# Copilot Instructions – CodeRetreat: Scramblecoin

## ⚠️ Assumptions Rule

**This applies to every agent and every session:**

> If you are about to make an assumption — about behaviour, design, scope, a rule interpretation, or anything not explicitly stated — **stop and ask first**, or **write the assumption explicitly in the issue/PR body** so it can be challenged.

Never silently assume. Never "pick the sensible default" without flagging it. A wrong silent assumption costs more time to fix than asking upfront.

---

## Project Goal

**Scramblecoin** is a bot-competition platform built for a CodeRetreat afternoon event. You build and host the game engine; colleagues each write a **bot** (in any language) that connects to your API, receives board state, and returns move decisions. Bots compete in a tournament bracket while everyone watches on a live spectator UI.

The complete game rules are in [`SCRAMBLECOIN_OVERVIEW.md`](../SCRAMBLECOIN_OVERVIEW.md) — always treat it as the authoritative source for game behaviour.

## How the event works

1. **You** run the Blazor Server app (game engine + API + spectator UI)
2. **Each team** writes a bot that calls your REST API (or connects via SignalR)
3. The bot receives the current board state as JSON, decides a move, and POSTs it back
4. The app enforces all rules — invalid moves are rejected
5. A **live leaderboard + spectator view** shows all games in progress
6. Games are organised in a **round-robin or knockout tournament**

## Tech Stack

| Concern | Technology |
|---------|-----------|
| UI + Spectator view | **Blazor Server** (.NET) |
| Architecture | **Clean Architecture** |
| Messaging | **MediatR** (commands & queries) |
| Bot communication | **REST API** (minimal API or controllers) |
| Live updates | **SignalR** (board state pushed to spectators) |
| Database | **SQL** via EF Core (game history, scores, leaderboard) |
| Testing | **xUnit** + **bUnit** (Blazor components) |

## Solution Structure

```
ScrambleCoin.sln
├── src/
│   ├── ScrambleCoin.Domain/          # Entities, Value Objects, Domain Events, Enums, Domain Interfaces
│   ├── ScrambleCoin.Application/     # MediatR Commands, Queries, Handlers, DTOs, Application Interfaces
│   ├── ScrambleCoin.Infrastructure/  # EF Core DbContext, Repositories, Migrations, SQL config
│   └── ScrambleCoin.Web/             # Blazor Server, REST API endpoints, SignalR hubs, DI wiring
└── tests/
    ├── ScrambleCoin.Domain.Tests/
    ├── ScrambleCoin.Application.Tests/
    ├── ScrambleCoin.Infrastructure.Tests/
    └── ScrambleCoin.Web.Tests/
```

## Key Domain Concepts

| Concept | Description |
|---------|-------------|
| `Board` | 8×8 grid holding tiles, obstacles, coins, and pieces |
| `Tile` | Single cell; may contain a coin, an obstacle, or a piece |
| `Piece` | A figurine with entry point, movement pattern, and optional ability |
| `Turn` | Coin spawn → place phase → move phase |
| `Move` | A step or jump by a piece; collects coins along the path (except Jump pieces) |
| `Ability` | Special effect on placement or after movement (Charge, Jump, Ethereal, ice patches, etc.) |
| `Game` | A match between two bots; has board, turn counter, scores, and status |
| `Tournament` | A set of games organised in rounds; tracks standings and leaderboard |

## Bot API Design

Bots interact via a simple REST API. The contract must stay **stable** — colleagues depend on it.

```
POST /api/games/{gameId}/join          → register a bot for a game
GET  /api/games/{gameId}/state         → get full board state as JSON
POST /api/games/{gameId}/move          → submit a move decision
GET  /api/tournament/leaderboard       → get current standings
```

**Board state response shape (example):**
```json
{
  "turn": 2,
  "yourScore": 3,
  "opponentScore": 1,
  "board": { "tiles": [...] },
  "yourPieces": [...],
  "opponentPieces": [...],
  "availableCoins": [...]
}
```

Breaking changes to this contract require a version bump (`/api/v2/...`).

## SignalR Hub

- `GameHub` pushes board state updates to all spectators after every move
- Spectator Blazor components subscribe via `HubConnection`
- Bots **do not** need to use SignalR — polling the REST API is fine

## Clean Architecture Rules

- **Domain** has zero dependencies on other projects — pure C#
- **Application** depends only on Domain; defines interfaces (e.g., `IGameRepository`) Infrastructure implements
- **Infrastructure** depends on Application and Domain; never referenced by Web directly for business logic
- **Web** depends on Application (dispatches MediatR requests); API controllers/endpoints only call `IMediator.Send()`

## MediatR Conventions

- **Commands** (write): `SubmitMoveCommand`, `StartGameCommand`, `JoinGameCommand`
- **Queries** (read): `GetBoardStateQuery`, `GetLeaderboardQuery`, `GetTournamentStandingsQuery`
- Handlers delegate game rules to Domain — **no game logic in handlers**
- Use `IRequest<TResponse>` for queries, `IRequest<Unit>` for fire-and-forget commands

## Database Conventions

- EF Core with SQL; `DbContext` in `Infrastructure`
- Persist: games, turns, moves, bot registrations, scores, leaderboard
- Repository interfaces in `Application`, implemented in `Infrastructure`
- Migrations in `Infrastructure`, applied at startup in development

## Build & Test Commands

```bash
dotnet build                                                              # build solution
dotnet test                                                               # run all tests
dotnet test --filter "FullyQualifiedName~<TestName>"                      # run a single test
dotnet run --project src/ScrambleCoin.Web                                 # run the app
dotnet ef migrations add <Name> --project src/ScrambleCoin.Infrastructure # add migration
dotnet ef database update --project src/ScrambleCoin.Infrastructure       # apply migrations
```

## Movement Rules (critical for implementation)

- **Orthogonal**: horizontal/vertical only
- **Diagonal**: diagonal only
- **Any direction**: free choice per step
- **Jump**: teleports to destination, ignores obstacles, collects coins **only at destination**
- **Charge**: moves until hitting an obstacle or board edge; distance not chosen by player
- **Ethereal**: passes through pieces/obstacles but **must end on a free tile**
- Pieces with **multiple moves** must use all of them
- **Ice patches** (Elsa): non-Jump pieces slide one extra tile; interrupts Charge and multi-move sequences
- Maximum **3 pieces per player** on the board simultaneously


| Concern | Technology |
|---------|-----------|
| UI | **Blazor Server** (.NET) |
| Architecture | **Clean Architecture** |
| Messaging | **MediatR** (commands & queries) |
| Database | **SQL** via EF Core (when persistence is needed) |
| Testing | **xUnit** + **bUnit** (Blazor components) |

## Solution Structure

```
ScrambleCoin.sln
├── src/
│   ├── ScrambleCoin.Domain/          # Entities, Value Objects, Domain Events, Enums, Domain Interfaces
│   ├── ScrambleCoin.Application/     # MediatR Commands, Queries, Handlers, DTOs, Application Interfaces
│   ├── ScrambleCoin.Infrastructure/  # EF Core DbContext, Repositories, Migrations, SQL config
│   └── ScrambleCoin.Web/             # Blazor Server — Pages, Components, DI wiring
└── tests/
    ├── ScrambleCoin.Domain.Tests/
    ├── ScrambleCoin.Application.Tests/
    ├── ScrambleCoin.Infrastructure.Tests/
    └── ScrambleCoin.Web.Tests/
```

## Clean Architecture Rules

- **Domain** has zero dependencies on other projects — pure C#, no EF, no MediatR
- **Application** depends only on Domain; defines interfaces (e.g., `IGameRepository`) that Infrastructure implements
- **Infrastructure** depends on Application and Domain; never referenced by Web directly for business logic
- **Web** depends on Application (dispatches MediatR requests); never calls repositories directly
- Cross-layer dependency violations must be caught in review

## MediatR Conventions

- **Commands** (write operations): `PlacePieceCommand`, `MovePieceCommand`, `StartGameCommand`
- **Queries** (read operations): `GetBoardStateQuery`, `GetGameScoreQuery`
- One handler per command/query, located in `Application`
- Handlers delegate business logic to Domain — **no game rules in handlers**
- Use `IRequest<TResponse>` for queries, `IRequest` or `IRequest<Unit>` for commands

## Database Conventions

- EF Core with SQL; `DbContext` lives in `Infrastructure`
- Repository interfaces defined in `Application`, implemented in `Infrastructure`
- Migrations live in `Infrastructure` and are applied at startup in development
- Do not use `DbContext` directly in Application or Web layers

## Build & Test Commands

```bash
dotnet build                                           # build solution
dotnet test                                            # run all tests
dotnet test --filter "FullyQualifiedName~<TestName>"   # run a single test
dotnet run --project src/ScrambleCoin.Web              # run the Blazor app
dotnet ef migrations add <Name> --project src/ScrambleCoin.Infrastructure  # add migration
dotnet ef database update --project src/ScrambleCoin.Infrastructure        # apply migrations
```

## CodeRetreat Context

- Sessions are **time-boxed** and focused on a specific practice (e.g., TDD, no primitives)
- Prefer **small, focused commits** per session
- Tests are the primary driver; implement only what a failing test requires

## Movement Rules (critical for implementation)

- **Orthogonal**: horizontal/vertical only
- **Diagonal**: diagonal only
- **Any direction**: free choice per step
- **Jump**: teleports to destination, ignores obstacles, collects coins **only at destination**
- **Charge**: moves in one direction until hitting an obstacle or board edge; player does not choose distance
- **Ethereal**: can pass through pieces and obstacles but **must end on a free tile**
- Pieces with **multiple moves** must use all of them
- **Ice patches** (Elsa): a non-Jump piece crossing an ice tile slides one extra tile in the same direction; interrupts Charge and multi-move sequences
- Maximum **3 pieces per player** on the board simultaneously
