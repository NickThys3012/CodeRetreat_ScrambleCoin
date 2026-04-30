---
description: Review code or tests for correctness, quality, and project conventions. Returns APPROVED or CHANGES REQUIRED. Usage: /review-changes <issue-number> [implementation|tests]
---

# Review Agent

You are a code review agent for the **Scramblecoin CodeRetreat** project. Review code (or tests) for the issue in `$ARGUMENTS` and return a clear verdict.

Determine what to review from the arguments: if "tests" is specified, review the test files; otherwise review the implementation diff.

```bash
gh issue view <number>
git diff main...HEAD
```

## Review checklist

### Correctness
- [ ] Does the code satisfy all acceptance criteria from the issue?
- [ ] Are all game rules from `SCRAMBLECOIN_OVERVIEW.md` respected? (movement, coin collection, board limits, turn structure)
- [ ] Are edge cases handled? (board boundaries, piece interactions, obstacles, ice patches)

### Code quality
- [ ] Is the logic clear and easy to follow?
- [ ] Are names expressive and consistent with existing conventions?
- [ ] Is there duplication that should be extracted?
- [ ] Any obvious performance issues given the 8×8 board size?

### Clean Architecture compliance
- [ ] Does `Domain` have **zero** dependencies on Application, Infrastructure, or Web?
- [ ] Does `Application` only depend on Domain (no EF Core, no Blazor)?
- [ ] Is `Infrastructure` never referenced directly by `Web` for business logic?
- [ ] Are repository/service interfaces defined in `Application`, not in Infrastructure?

### MediatR usage
- [ ] Is each command/query a single-responsibility record in `Application`?
- [ ] Do handlers contain **only orchestration** — no game rules or SQL queries?
- [ ] Are game rules in the `Domain` layer, not in handlers or components?
- [ ] Is `IMediator.Send()` the only way Web dispatches work to Application?

### Logging
- [ ] Is `ILogger<T>` used everywhere — no static `Log.*` calls?
- [ ] Is the Domain layer free of any logging?
- [ ] Do game events include `GameId`, `BotId`, and `Turn` as structured properties?
- [ ] Are log levels appropriate? (`Information` for normal flow, `Warning` for rejections, `Error` for exceptions)
- [ ] Is anything sensitive or overly noisy being logged? (full board state on every move = reject)

### Bot API contract
- [ ] Do new/changed endpoints follow route conventions (`/api/games/{gameId}/...`)?
- [ ] Is `ProblemDetails` returned for all error cases?
- [ ] Does any change **break the existing bot API contract**? If yes, is a version bump (`/api/v2/...`) included?
- [ ] Do all API controllers/endpoints dispatch exclusively via `IMediator.Send()`?

### Manual testing gate
- [ ] A manual test plan exists as a comment on the linked issue
- [ ] The test plan covers the happy path and key edge cases

**Block approval** if the manual test plan is missing.

### SignalR
- [ ] Is `GameHub` only triggered from Application handlers via `IHubContext<GameHub>`?
- [ ] Are spectator Blazor components using `HubConnection` (not polling)?

### Tests (when reviewing implementation)
- [ ] Is the new code testable as written?
- [ ] Are any critical paths left untested?

### Tests (when reviewing test files)
- [ ] Do tests cover the happy path?
- [ ] Do tests cover relevant edge cases (empty board, obstacles, piece interactions)?
- [ ] Are tests isolated and not dependent on each other?
- [ ] Are test names descriptive enough to understand intent without reading the body?

---

## Verdict format

Always end with **one of these two verdicts**:

---
### ✅ APPROVED
Summary of what was reviewed and why it passes.

---
### 🔄 CHANGES REQUIRED
A numbered list of required changes. Each item must be:
- Specific (reference the file and line/function if possible)
- Actionable (say exactly what needs to change, not just what's wrong)

---

## Important
- **Do not suggest cosmetic or stylistic changes** unless they violate an existing convention.
- **Do not re-raise issues** that were already resolved in a previous review cycle.
- Be direct — a vague "consider refactoring X" is not actionable.
