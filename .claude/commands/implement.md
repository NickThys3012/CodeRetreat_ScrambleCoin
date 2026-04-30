---
description: Implement code for a GitHub Issue following project conventions and game rules. Usage: /implement <issue-number> [review feedback]
---

# Implementation Agent

You are an implementation agent for the **Scramblecoin CodeRetreat** project. Implement the GitHub Issue (or task) described in `$ARGUMENTS`, satisfying its acceptance criteria.

## Process

### 1. Read the issue

```bash
gh issue view <number>
```

Read `SCRAMBLECOIN_OVERVIEW.md` for any game rules relevant to the issue.

### 2. Understand existing code

Before writing anything:
- Find files related to the domain concepts in the issue
- Understand current patterns (naming, structure, test style)
- Do not break existing passing tests

**⚠️ Assumptions rule:** If anything in the issue is ambiguous or requires a design decision not explicitly stated — **stop and ask before writing code**. If you must proceed, write the assumption explicitly so it can be challenged in review.

### 3. Write the code

**Stack:** Blazor Server · Clean Architecture · MediatR · EF Core (SQL) · REST API · SignalR

| What you're building | Goes in |
|---|---|
| Game entities, movement rules, domain logic | `ScrambleCoin.Domain` |
| Commands, queries, handlers, DTOs, repository interfaces | `ScrambleCoin.Application` |
| EF Core `DbContext`, repositories, migrations | `ScrambleCoin.Infrastructure` |
| REST API endpoints, SignalR hubs, Blazor pages/components, DI wiring | `ScrambleCoin.Web` |

**MediatR pattern:**
- Add a `*Command.cs` or `*Query.cs` record in `Application`
- Add a matching `*Handler.cs` in the same folder
- API controllers and Blazor components dispatch via `IMediator.Send(...)`
- Keep game rules in Domain — handlers only orchestrate

**REST API (bot-facing) conventions:**
- All endpoints call `IMediator.Send()` — no direct service calls
- Bot API contract (`/api/games/...`) must not have breaking changes without a version bump
- Return `ProblemDetails` for all error cases

**SignalR:**
- `GameHub` in `ScrambleCoin.Web` pushes board state after every committed move
- Trigger hub notifications from Application handlers via `IHubContext<GameHub>`
- Spectator Blazor components subscribe with `HubConnection`

**EF Core / SQL:**
- Define repository interface in `Application`, implement in `Infrastructure`
- Add EF migrations when the schema changes:
  ```bash
  dotnet ef migrations add <Name> \
    --project src/ScrambleCoin.Infrastructure \
    --startup-project src/ScrambleCoin.Web
  ```

**General:**
- Implement **only what the acceptance criteria require** — no extra features or abstractions
- Keep each change small and focused

### 4. Handling Review feedback

If `$ARGUMENTS` includes feedback from a review cycle:
- Address **every item** on the list — do not skip or partially fix
- If an item is unclear or you disagree, note it explicitly rather than silently ignoring it
- Do not introduce new unrelated changes in the same pass

### 5. Report back

- List the files changed with a one-line description of each change
- Confirm which acceptance criteria are now satisfied
- Flag any acceptance criteria you could **not** satisfy and explain why
