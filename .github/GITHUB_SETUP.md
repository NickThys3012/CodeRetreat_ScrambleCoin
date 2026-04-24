# GitHub Setup Guide – Scramblecoin

This document walks through the full GitHub setup for the Scramblecoin bot-competition platform. Follow the steps in order.

---

## 1. Push the repository

```bash
git add .
git commit -m "chore: initial repository setup

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
git push -u origin main
```

---

## 2. Labels

Delete GitHub's default labels and create the ones below. Run this script from the repo root (requires `gh` CLI):

```bash
# Delete all default labels
gh label list --json name -q '.[].name' | xargs -I {} gh label delete "{}" --yes

# Create project labels
gh label create "feature"     --color "0075ca" --description "New game mechanic or platform behaviour"
gh label create "bug"         --color "d73a4a" --description "Incorrect behaviour vs SCRAMBLECOIN_OVERVIEW.md"
gh label create "refactor"    --color "e4e669" --description "Structural improvement, no behaviour change"
gh label create "test"        --color "bfd4f2" --description "Test coverage only"
gh label create "api"         --color "f9a03f" --description "Bot-facing REST API endpoints"
gh label create "signalr"     --color "b60205" --description "Live spectator / push updates"
gh label create "tournament"  --color "5319e7" --description "Matchmaking, brackets, leaderboard"
gh label create "ui"          --color "0e8a16" --description "Blazor spectator components"
gh label create "domain"      --color "c5def5" --description "Core game engine / rules"
gh label create "infra"       --color "fef2c0" --description "EF Core, SQL, migrations"
gh label create "blocked"     --color "e11d48" --description "Cannot proceed, needs input"
gh label create "good first issue" --color "7057ff" --description "Good starting point"
```

---

## 3. Milestones

Create milestones that map to the event phases:

```bash
gh api repos/NickThys3012/CodeRetreat_ScrambleCoin/milestones \
  --method POST -f title="🏗️ Project Setup" \
  -f description="Solution scaffolding, CI/CD pipeline, NuGet packages, EF Core config, initial DB schema, DI wiring"

gh api repos/NickThys3012/CodeRetreat_ScrambleCoin/milestones \
  --method POST -f title="🎮 Game Engine Core" \
  -f description="Board, tiles, obstacles, basic piece movement, coin spawning, turn structure"

gh api repos/NickThys3012/CodeRetreat_ScrambleCoin/milestones \
  --method POST -f title="🤖 Bot API" \
  -f description="REST endpoints for bot registration, board state, and move submission"

gh api repos/NickThys3012/CodeRetreat_ScrambleCoin/milestones \
  --method POST -f title="🎭 Piece Abilities" \
  -f description="All special abilities: Charge, Jump, Ethereal, ice patches, pushes, buffs"

gh api repos/NickThys3012/CodeRetreat_ScrambleCoin/milestones \
  --method POST -f title="🏆 Tournament & Leaderboard" \
  -f description="Matchmaking, round-robin/knockout bracket, live leaderboard, SignalR spectator view"

gh api repos/NickThys3012/CodeRetreat_ScrambleCoin/milestones \
  --method POST -f title="🎉 Event Ready" \
  -f description="Polish, stress testing, bot starter kit, documentation for participants"
```

> These 6 milestones are **fixed**. The Planning Agent will always assign issues to one of these — never create new ones without explicit approval.

---

## 4. GitHub Project (Projects V2)

### 4.1 Create the project

1. Go to **github.com/NickThys3012** → **Projects** → **New project**
2. Choose **Board** template
3. Name it: `Scramblecoin – Bot Competition`
4. Set visibility to **Private** (or Public if you want colleagues to see it)
5. Click **Create**

### 4.2 Custom fields

Add the following fields via **⚙️ Settings → Fields → New field**:

| Field name | Type | Options |
|------------|------|---------|
| `Status` | Single select | `📋 Backlog` · `🔜 Ready` · `🚧 In Progress` · `👀 In Review` · `🧪 Needs Manual Test` · `✅ Done` |
| `Layer` | Single select | `Domain` · `Application` · `Infrastructure` · `Web/API` · `SignalR` · `Tournament` · `UI` |
| `Priority` | Single select | `🔴 High` · `🟡 Medium` · `🟢 Low` |
| `Size` | Single select | `XS` · `S` · `M` · `L` · `XL` |

> **Milestone** is a default GitHub field — just click **+ Add field** and select it from the built-in options. No need to create it manually.

### 4.3 Views

Create these views (via **+ New view**):

| View name | Type | Group by | Filter |
|-----------|------|----------|--------|
| `🗂️ Board` | Board | `Status` | *(none)* |
| `📅 Milestone` | Board | `Milestone` | *(none)* |
| `🧱 By Layer` | Board | `Layer` | *(none)* |
| `🔴 Backlog` | Table | *(none)* | `Status = Backlog`, sorted by Priority |
| `🚧 Active` | Table | *(none)* | `Status = In Progress OR In Review` |
| `🧪 Needs Manual Test` | Table | *(none)* | `Status = Needs Manual Test` |

### 4.4 Link the repository

In the project: **⚙️ Settings → Linked repositories → Add repository** → select `ScrambleCoin`.

New issues and PRs will now appear in the project automatically.

### 4.5 Automation

In **⚙️ Settings → Workflows**, enable:

| Workflow | Action |
|----------|--------|
| `Item added to project` | Set status → `📋 Backlog` |
| `Item reopened` | Set status → `🔜 Ready` |
| `Pull request merged` | Set status → `✅ Done` |
| `Item closed` | Set status → `✅ Done` |

> The `🧪 Needs Manual Test` status is set by the Orchestrator after the Testing Agent completes. Every PR goes through this status — no exceptions.

---

## 5. Branch Protection

Set up branch protection for `main`:

```bash
gh api repos/NickThys3012/CodeRetreat_ScrambleCoin/branches/main/protection \
  --method PUT \
  --header "Accept: application/vnd.github+json" \
  --input - <<'EOF'
{
  "required_status_checks": {
    "strict": true,
    "contexts": []
  },
  "required_pull_request_reviews": {
    "required_approving_review_count": 1,
    "dismiss_stale_reviews": true
  },
  "enforce_admins": false,
  "restrictions": null
}
EOF
```

Or via the UI: **Settings → Branches → Add branch protection rule**:
- Branch name pattern: `main`
- ✅ Require a pull request before merging (1 approval)
- ✅ Dismiss stale pull request approvals when new commits are pushed
- ✅ Require status checks to pass (add your CI checks once set up)
- ✅ Do not allow bypassing the above settings

---

## 6. Issue Templates

Three templates are included in `.github/ISSUE_TEMPLATE/`:

| File | Use for |
|------|---------|
| `feature.md` | New features or game mechanics |
| `bug.md` | Rule violations or broken behaviour |
| `bot-api.md` | Changes to the bot-facing REST API contract |

---

## 7. Pull Request Template

A PR template is included at `.github/pull_request_template.md`. It prompts for:
- Summary + linked issue
- Layer(s) touched
- Testing evidence
- API contract change flag

---

## 8. Recommended workflow

```
Planning Agent → GitHub Issue (auto-added to Project as Backlog)
       ↓
Move to "Ready" when you want to start it
       ↓
Orchestrator Agent → feature branch → implementation → review → tests → PR
       ↓
PR reviewed → merged → auto-moved to "Done" in Project
```

---

## 9. Bot starter kit (for the event day)

Before the event, create a `bot-starter/` folder in the repo containing:
- `README.md` explaining the API contract
- Example bots in 2–3 languages (C#, Python, JavaScript)
- A Postman collection or `.http` file for manual testing

This lets colleagues get started immediately without reading the full codebase.
