# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Assumptions Rule

If you are about to make an assumption — about behaviour, design, scope, or a rule interpretation — **stop and ask first**, or **write the assumption explicitly** so it can be challenged. Never silently pick a "sensible default."

## Project Overview

**ScrambleCoin** is a bot-competition platform for a CodeRetreat event. The app hosts the game engine, a REST API (for bots), a SignalR hub (for spectators), and a live Blazor Server UI. The authoritative game rules are in [`SCRAMBLECOIN_OVERVIEW.md`](SCRAMBLECOIN_OVERVIEW.md).

## Commands

```bash
# Development
dotnet restore
dotnet build
dotnet run --project src/ScrambleCoin.Web          # https://localhost:7102

# Testing
dotnet test                                         # all tests
dotnet test tests/ScrambleCoin.Domain.Tests         # single project
dotnet test --filter "FullyQualifiedName~<Name>"    # single test

# EF Core
dotnet ef migrations add <Name> \
  --project src/ScrambleCoin.Infrastructure \
  --startup-project src/ScrambleCoin.Web
dotnet ef database update \
  --project src/ScrambleCoin.Infrastructure \
  --startup-project src/ScrambleCoin.Web

# E2E (Playwright — requires running app first)
dotnet build tests/ScrambleCoin.E2E.Tests
pwsh tests/ScrambleCoin.E2E.Tests/bin/Debug/net9.0/playwright.ps1 install
dotnet test tests/ScrambleCoin.E2E.Tests
```

## Local Setup

```bash
docker compose up -d    # SQL Server 2022 on localhost,1433; wait ~15s
cp src/ScrambleCoin.Web/appsettings.Development.example.json \
   src/ScrambleCoin.Web/appsettings.Development.json
dotnet ef database update \
  --project src/ScrambleCoin.Infrastructure \
  --startup-project src/ScrambleCoin.Web
```

`appsettings.Development.json` is gitignored.

## Architecture

Clean Architecture with MediatR CQRS. Layer dependency order (each only depends on layers to its right):

```
Web → Application → Domain
Infrastructure → Application, Domain
```

**Domain** — pure C#; zero dependencies. Entities: `Game`, `Board`, `Tile`, `Piece`. Value objects: `Position`, `Lineup`. Domain events. Enums. **No logging here.**

**Application** — MediatR commands/queries and handlers; repository interfaces (`IGameRepository`). Handlers delegate all game rules to Domain — **no game logic in handlers**.

**Infrastructure** — EF Core `ScrambleCoinDbContext`, SQL Server repositories, migrations. Never reference `DbContext` directly from Application or Web.

**Web** — Blazor Server pages/components, REST API endpoints (`/api/games/{id}/...`), SignalR `GameHub`, DI wiring in `Program.cs`. Controllers/endpoints only call `IMediator.Send()`.

## MediatR Conventions

- Commands (writes): `SubmitMoveCommand`, `StartGameCommand`, `PlacePieceCommand`
- Queries (reads): `GetBoardStateQuery`, `GetLeaderboardQuery`
- `IRequest<TResponse>` for queries; `IRequest<Unit>` for commands
- One handler per command/query, in `Application`

## Bot API Contract

The REST contract must remain stable — breaking changes require a version bump (`/api/v2/...`).

```
POST /api/games/{gameId}/join
GET  /api/games/{gameId}/state
POST /api/games/{gameId}/move
GET  /api/tournament/leaderboard
```

## Movement Rules (critical)

- **Orthogonal** — horizontal/vertical only
- **Diagonal** — diagonal only
- **Jump** — teleports to destination, ignores obstacles, collects coins **only at destination**
- **Charge** — moves in one direction until hitting obstacle or edge; player does not choose distance
- **Ethereal** — passes through pieces/obstacles but **must end on a free tile**
- Pieces with multiple moves must use all of them
- **Ice patches** (Elsa) — non-Jump piece crossing ice slides one extra tile; interrupts Charge and multi-move sequences
- Maximum **3 pieces per player** on the board simultaneously

## Logging

Use `ILogger<T>` everywhere (injected). Never use `Log.*` static methods. Domain has zero logging.

Always include these structured properties on game events:
```csharp
_logger.ForContext("GameId", gameId)
       .ForContext("BotId", botId)
       .ForContext("Turn", turnNumber)
       .LogInformation("Move submitted: {Move}", move);
```

Log levels: `Information` for game lifecycle events, `Warning` for invalid/rejected moves, `Error` for exceptions, `Debug` for local-dev detail only.

## Testing Strategy

| Project | Approach |
|---------|----------|
| `Domain.Tests` | Pure xUnit, no mocks |
| `Application.Tests` | xUnit + NSubstitute mocks |
| `Infrastructure.Tests` | EF Core against Docker SQL Server |
| `Web.Tests` | bUnit (Blazor components) + WebApplicationFactory (API) |
| `E2E.Tests` | Playwright against running app |

CI enforces **85% code coverage** (configured in `coverlet.runsettings`).
