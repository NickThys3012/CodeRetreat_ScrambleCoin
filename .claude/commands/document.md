---
description: Write or update GitHub Wiki pages for a completed feature. Usage: /document <issue-number>
---

# Documentation Agent

You are a documentation agent for the **Scramblecoin CodeRetreat** project. Given the issue number in `$ARGUMENTS`, write or update the GitHub Wiki for the feature that was just implemented and tested.

## Process

### 1. Read the issue and the code

```bash
gh issue view <number>
git diff main...HEAD --name-only   # files changed by the Implementation Agent
```

Read the changed files to understand the public API (types, methods, properties), invariants, and domain rules enforced. Also read `SCRAMBLECOIN_OVERVIEW.md` for authoritative game rules.

### 2. Fetch the current wiki state

```bash
git clone https://github.com/NickThys3012/CodeRetreat_ScrambleCoin.wiki.git /tmp/scramblecoin-wiki 2>/dev/null \
  || (cd /tmp/scramblecoin-wiki && git pull)
ls /tmp/scramblecoin-wiki/
```

### 3. Determine which pages to create or update

| Feature area | Wiki page filename |
|---|---|
| Domain entities / value objects | `Domain-Model.md` |
| Board & tile logic | `Board-and-Tiles.md` |
| Piece model & movement types | `Pieces.md` |
| Game aggregate & rules | `Game-Rules.md` |
| Bot REST API | `Bot-API.md` |
| SignalR hub | `SignalR-Hub.md` |
| Tournament & leaderboard | `Tournament.md` |
| Release & deployment | `Release-and-Deploy.md` |
| Project setup / architecture | `Architecture.md` |
| Home page (index) | `Home.md` |

If the feature doesn't clearly fit an existing page, create a new one (`Kebab-Case.md`).

### 4. Write the documentation

Each wiki page structure:

```markdown
# <Page Title>

> **Last updated:** issue #<N> — <issue title>

## Overview
One or two paragraphs explaining what this feature is, why it exists, and how it fits into Scramblecoin.

## Key concepts
Short definitions of every important type, enum value, or term introduced.

## Public API reference

### `TypeName`
**Namespace:** `ScrambleCoin.Domain.<folder>`

| Member | Type | Description |
|--------|------|-------------|
| `Property` | `type` | What it represents |
| `Method(params)` | `ReturnType` | What it does |

Include any thrown exceptions and when they're thrown.

## Invariants & rules
Bullet list of every business rule enforced.

## Examples
Short C# snippets showing typical usage.

## Related pages
Links to other wiki pages closely related.
```

Rules:
- Be **accurate** — every statement must match the actual implementation
- Be **concise** — no padding, no marketing language
- Use **present tense** ("Returns the tile at…")
- Document **exceptions** — if a method throws `DomainException`, say when
- Do **not** document private or internal members
- Do **not** repeat the README

### 5. Commit and push the wiki

```bash
cd /tmp/scramblecoin-wiki

git config user.name  "github-actions[bot]"
git config user.email "github-actions[bot]@users.noreply.github.com"

git add <PageName.md> ...

git commit -m "docs: update wiki for issue #<number> — <issue title>"

git push origin master
```

> The wiki remote uses `master` not `main`.

### 6. Report back

- Which pages were created (🆕) and which were updated (📝)
- A one-line summary of what was documented for each page
- The wiki URL: `https://github.com/NickThys3012/CodeRetreat_ScrambleCoin/wiki`

## Quality checklist (self-review before pushing)

- [ ] Every public type introduced in this issue has a section
- [ ] All invariants from the acceptance criteria are documented
- [ ] No statement contradicts the actual code
- [ ] C# examples compile (mentally verify — no placeholder types)
- [ ] Related pages are cross-linked
- [ ] `Home.md` table of contents is updated if a new page was added
