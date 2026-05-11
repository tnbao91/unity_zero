# ClaudeMemory — AI agent context for your game

This sample bundle gives Claude Code (and other AI coding agents) the context they need to help extend the Zero template in YOUR game.

## What's inside

| File | Purpose |
|---|---|
| `CLAUDE.md` | Top-level brief Claude reads at session start. Stack constraints, architecture, common tasks. |
| `claude-context/architecture.md` | Asmdef tier diagram, peer rule, DI flow, bootstrap pipeline order. |
| `claude-context/available-services.md` | All ~28 service interfaces with signatures + notes. |
| `claude-context/extension-points.md` | Concrete recipes: swap mock SDK, add state, add popup, add cheat command, etc. |
| `claude-context/stack-constraints.md` | What's locked and why. Pushback rules when AI suggests substitutes. |
| `claude-context/pitfalls.md` | Top consumer-relevant footguns. Each came from a real bug. |
| `.claude/agents/*.md` | Six pre-built Claude Code sub-agents tuned for game-on-Zero work: `game-lead` (opus, architecture), `game-senior` (sonnet, implementation), `game-junior` (haiku, boilerplate), plus three specialists — `asmdef-boundary-reviewer`, `service-scaffolder`, `pitfalls-guard`. Pick the right tier per task; specialists are auto-suggested for boundary/footgun review. |
| `.claude/settings.example.json` | Permissions skeleton for fewer prompt interruptions. |

## How to use

1. **Import** this sample via Package Manager (Zero → Samples → ClaudeMemory).
2. **Move** `CLAUDE.md` from `Assets/Samples/com.tnbao91.nobody.zero/<version>/ClaudeMemory/` to your repo root (next to `.git/`).
3. **Move** the `claude-context/` folder to your repo root too.
4. **Move** `.claude/settings.example.json` to `.claude/settings.json` at repo root (adjust permissions for your team's risk tolerance).
5. **Move** the entire `.claude/agents/` folder to your repo root (next to `.claude/settings.json`). Verify with `/agents` inside Claude Code — the six game agents should appear.
6. **Delete** `Assets/Samples/com.tnbao91.nobody.zero/<version>/ClaudeMemory/` — the originals stay shipped in the package, you only need the copies at repo root.

After this, when Claude Code starts in your repo, it auto-reads `CLAUDE.md` and finds the rest via the relative paths.

## Updates

When the Zero package version bumps, re-import the sample to get the latest CLAUDE.md / context files. Diff against your current copies and merge any changes you care about — your customizations to `CLAUDE.md` (game-specific rules, team conventions) are yours to maintain.

## Why a sample, not auto-install

Unity Package Manager's "Import Sample" pattern keeps the package package-clean and lets consumers opt in. If we put `CLAUDE.md` directly at the package root, every consumer would inherit it whether they use AI agents or not — and they couldn't customize it without editing inside `Library/PackageCache/`.

Sample-based opt-in = consumer owns their copies = updates are diff-able, customizations persist.
