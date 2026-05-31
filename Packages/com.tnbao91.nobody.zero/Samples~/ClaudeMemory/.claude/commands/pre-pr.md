---
description: Pre-PR review for game code — fan out asmdef-boundary-reviewer + pitfalls-guard on the diff, then summarize.
argument-hint: [base-branch, default main]
allowed-tools: Read, Grep, Glob, Bash(git diff:*), Bash(git log:*), Bash(git status)
---

Run the AI-side pre-PR review gate from `CLAUDE.md` against base branch **${ARGUMENTS:-main}**, for code in YOUR game asmdefs (not the package).

1. Establish the diff: `git diff ${ARGUMENTS:-main} --stat`.
2. Spawn **both** consumer-tuned specialist agents in parallel on this diff (read-only):
   - `asmdef-boundary-reviewer` — peer rule in YOUR asmdefs (`Game.Gameplay`/`Game.UI`/`Game.Meta` don't cross-ref), no game asmdef referencing `Zero.Bootstrap`, `autoReferenced: false` on new asmdefs, missing transitive refs.
   - `pitfalls-guard` — the footguns in `claude-context/pitfalls.md` (legacy `Input.*`, async test pattern, `using R3;`, `dynamic`, EditMode `Destroy`, Addressables `HasKeyAsync`, `RegisterType` w/ primitive).
3. Then run `/code-review` on the same diff for correctness + simplification findings.
4. Collate into one report: **PASS / FAIL / WARN**, grouped by severity (P0 blocker / P1 must-fix / P2 recommend), each with `file:line` + suggested fix. Do NOT edit — hand the fix list to `game-senior` / `game-junior`.

Remind me: confirm nothing under `Packages/com.tnbao91.nobody.zero/**` changed, verify in Editor (Press Play `Bootstrap.unity` + Test Runner via Unity MCP), and never push without my explicit go-ahead.
