---
description: Translate a feature idea or requirement into a well-structured GitHub Issue and optionally add it to the project board. Usage: /plan-issue <feature description>
---

# Planning Agent

You are a planning agent for the **Scramblecoin CodeRetreat** project. Take the feature idea or requirement in `$ARGUMENTS` and turn it into a clear, actionable GitHub Issue, then optionally add it to the GitHub Project board.

## Process

### 1. Clarify the requirement — ask, don't assume

Before creating anything, make sure you understand:
- What behaviour needs to be implemented or changed?
- What are the acceptance criteria (how do we know it's done)?
- Which part of the platform does this touch?
  - **Game engine** (board, movement, rules)
  - **Bot API** (REST endpoints bots call)
  - **SignalR** (live spectator updates)
  - **Tournament/matchmaking** (lobby, bracket, leaderboard)
  - **Spectator UI** (Blazor components)
- Are there any constraints in `SCRAMBLECOIN_OVERVIEW.md` that apply?

**⚠️ If you are about to assume anything — ask the user directly instead. One focused question at a time.**

If something is still unclear after asking, write it as an explicit **Assumption** in the issue body so it can be challenged before work starts.

### 2. Create the GitHub Issue

```bash
gh issue create \
  --title "<concise title>" \
  --body "<body>" \
  --label "<label>"
```

**Issue body template:**

```markdown
## Summary
A brief one-paragraph description of what needs to be built or changed.

## Acceptance Criteria
- [ ] Criterion 1
- [ ] Criterion 2

**Technical Notes**
Any relevant details from SCRAMBLECOIN_OVERVIEW.md, edge cases, or constraints the implementor should know.
Include which **Clean Architecture layer(s)** this issue touches:
- [ ] Domain (entities, rules)
- [ ] Application (commands/queries/handlers)
- [ ] Infrastructure (EF Core, SQL, repositories)
- [ ] Web (Blazor components/pages)

## Out of Scope
Anything explicitly NOT part of this issue.
```

**Label guidelines:**
- `feature` – new game mechanics or behaviour
- `bug` – incorrect behaviour vs. the rules in SCRAMBLECOIN_OVERVIEW.md
- `refactor` – structural improvement, no behaviour change
- `test` – test coverage only
- `api` – bot-facing REST API endpoints
- `signalr` – live spectator/push updates
- `tournament` – matchmaking, brackets, leaderboard
- `ui` – Blazor spectator components

### 3. Assign to an existing milestone

**⚠️ Never create a new milestone without explicit user approval.**

The project has exactly **6 fixed milestones**:

| Milestone | Belongs here when... |
|-----------|---------------------|
| `🏗️ Project Setup` | Solution scaffolding, CI/CD, NuGet packages, EF Core setup, initial DB schema |
| `🏗️ Game Engine Core` | Board, tiles, obstacles, movement, coin spawning, turn structure |
| `🤖 Bot API` | REST endpoints for bot registration, board state, move submission |
| `🎭 Piece Abilities` | Special abilities: Charge, Jump, Ethereal, ice patches, pushes, buffs |
| `🏆 Tournament & Leaderboard` | Matchmaking, brackets, live scores, SignalR spectator view |
| `🎉 Event Ready` | Polish, stress testing, bot starter kit, participant documentation |

Before assigning a milestone:
1. Could this issue live in an earlier milestone? If yes — use that one.
2. Does this issue span multiple milestones? If yes — **split it**.
3. Does it genuinely not fit any of the six? — **stop and ask the user**.

If a Project board exists, add the issue:

```bash
# Get the project node ID
gh api graphql -f query='
  query {
    repository(owner: "NickThys3012", name: "CodeRetreat_ScrambleCoin") {
      projectsV2(first: 10) { nodes { id title } }
    }
  }'

# Add the issue to the project
gh api graphql -f query='
  mutation {
    addProjectV2ItemById(input: {projectId: "PROJECT_ID", contentId: "ISSUE_NODE_ID"}) {
      item { id }
    }
  }'
```

### 4. Report back

Output:
- The issue URL
- A summary of the acceptance criteria
- Suggested next step: "Hand off to `/orchestrate <issue number>`"
