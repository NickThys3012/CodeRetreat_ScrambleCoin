---
description: Run the full implementation pipeline for a GitHub Issue (implement → review → test → review → document → PR). Usage: /orchestrate <issue-number>
---

# Orchestrator Agent

You are the orchestrator for the **Scramblecoin CodeRetreat** project. Given the GitHub Issue number in `$ARGUMENTS`, coordinate the full delivery pipeline — from first line of code to an open Pull Request.

## Pipeline

```
Issue
  └─► [1] Implement
        └─► [2] Review (implementation)
              ├─ CHANGES REQUIRED → back to [1]  (max 2 times)
              ├─ ESCALATE after 2 failed cycles  → STOP, report to user
              └─ APPROVED
                    └─► [3] Write tests
                          └─► [4] Review (tests)
                                ├─ CHANGES REQUIRED → back to [3]  (max 2 times)
                                ├─ ESCALATE after 2 failed cycles  → STOP, report to user
                                └─ APPROVED
                                      └─► [5] Document
                                                └─► [6] Open Pull Request
```

---

## Step 0 — Setup

```bash
gh issue view <number>
```

**⛔ Check project board status before doing anything:**

```bash
gh api graphql -f query='
  query {
    repository(owner: "NickThys3012", name: "CodeRetreat_ScrambleCoin") {
      issue(number: <number>) {
        projectItems(first: 5) {
          nodes {
            fieldValues(first: 10) {
              nodes {
                ... on ProjectV2ItemFieldSingleSelectValue {
                  name
                  field { ... on ProjectV2SingleSelectField { name } }
                }
              }
            }
          }
        }
      }
    }
  }'
```

If the `Status` field is **`Backlog`** — **STOP immediately**:

> ⛔ Issue #N is in **Backlog** status and cannot be implemented yet.
> Move it to **Ready** on the project board before running the orchestrator.

Only continue if status is `Ready`, `In Progress`, `🧪 Needs Manual Test`, or the issue is not on a board.

Create the feature branch:
```bash
git checkout main && git pull
git checkout -b feature/issue-<number>-<short-slug>
```

Track two counters: `impl_cycles = 0`, `test_cycles = 0`

---

## Step 1 — Implementation

Follow the instructions in `.claude/commands/implement.md` for this issue (with empty review feedback on the first pass).

Commit after implementation:
```bash
git add -p   # stage only relevant files
git commit -m "feat: <short description> (closes #<number>)

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>"
```

---

## Step 2 — Implementation Review

Follow the instructions in `.claude/commands/review-changes.md` with:
- The diff since the branch was created
- Context: "reviewing implementation"
- The issue number

**APPROVED** → proceed to Step 3.

**CHANGES REQUIRED** → increment `impl_cycles`. If `impl_cycles < 2`, return to Step 1 with feedback. If `impl_cycles >= 2`:

> ⚠️ Implementation went through 2 review cycles with unresolved issues. Human input needed.
>
> Outstanding issues:
> [numbered list from review]

---

## Step 3 — Testing

Follow the instructions in `.claude/commands/write-tests.md` for this issue and the list of changed files.

The Testing step will also post a manual test plan as a comment on the issue and set the project status to `🧪 Needs Manual Test`.

Commit tests:
```bash
git add -p
git commit -m "test: add tests for <short description> (#<number>)

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>"
```

---

## Step 4 — Test Review

Follow the instructions in `.claude/commands/review-changes.md` with:
- The test files added/modified
- Context: "reviewing tests"
- The issue number

**APPROVED** → proceed to Step 5.

**CHANGES REQUIRED** → increment `test_cycles`. If `test_cycles < 2`, return to Step 3 with feedback. If `test_cycles >= 2`:

> ⚠️ Tests went through 2 review cycles with unresolved issues. Human input needed.
>
> Outstanding issues:
> [numbered list from review]

---

## Step 5 — Documentation

Follow the instructions in `.claude/commands/document.md` for this issue and the list of changed files.

---

## Step 6 — Open Pull Request

**Step 6a — Determine the version label** from issue labels:

| Issue label | Version label |
|-------------|---------------|
| `bug` | `patch` |
| `refactor` | `patch` |
| `test` | `patch` |
| `feature` | `minor` |
| `api` | `minor` |
| `signalr` | `minor` |
| `tournament` | `minor` |
| `ui` | `minor` |
| _(none / unknown)_ | `patch` |

`major` is never applied automatically — flag it in the PR body if you believe changes are breaking.

```bash
gh issue view <number> --json labels -q '.labels[].name'
```

**Step 6b — Push and create the PR:**

```bash
git push origin feature/issue-<number>-<short-slug>

gh pr create \
  --title "<issue title>" \
  --label "<patch|minor>" \
  --body "$(cat <<'EOF'
## Summary
Closes #<number>.

## Changes
- <file 1>: <what changed>
- <file 2>: <what changed>

## Testing
- Unit tests: X added
- Integration tests: X added
- E2E tests: X added

All tests pass ✅

## Review cycles
- Implementation: <impl_cycles> cycle(s)
- Tests: <test_cycles> cycle(s)

## Version bump
<!-- version label applied automatically based on issue labels -->
EOF
)" \
  --base main \
  --head feature/issue-<number>-<short-slug>
```

Always use `Closes #N` in the PR body — never `Implements #N` or `Fixes #N` unless it's a bug fix.

Report the PR URL to the user and mark the pipeline complete.

---

## Loop-prevention rules

| Counter | Max | Action when exceeded |
|---------|-----|----------------------|
| `impl_cycles` | 2 | Stop, escalate to user |
| `test_cycles` | 2 | Stop, escalate to user |

Never loop back without incrementing the counter. Never reset a counter mid-pipeline.
