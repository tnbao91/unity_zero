---
description: Pre-PR review — fan out asmdef-boundary-reviewer + pitfalls-guard on the diff, then summarize.
argument-hint: [base-branch, default main]
allowed-tools: Read, Grep, Glob, Bash(git diff:*), Bash(git log:*), Bash(git status)
---

Run the AI-side pre-PR review gate from `CLAUDE.md` → "Automated gate before PR" against base branch **${ARGUMENTS:-main}**.

1. Establish the diff: `git diff ${ARGUMENTS:-main} --stat` so the reviewers know what changed.
2. Spawn **both** specialist agents in parallel on this diff (they are read-only):
   - `asmdef-boundary-reviewer` — peer-rule violations, missing transitive refs, `autoReferenced` regressions, `Zero.Core` leaking impl deps.
   - `pitfalls-guard` — the footgun catalog (`docs/dev/PITFALLS.md`). Note the warn-only hook already nudged on the context-free subset during editing; the agent is the full-diff gate and owns the judgment checks.
3. Then run `/code-review` on the same diff for correctness + simplification findings.
4. Collate into one report: **PASS / FAIL / WARN**, findings grouped by severity (P0 blocker / P1 must-fix / P2 recommend), each with `file:line` and a suggested fix. Do NOT edit — hand the fix list back so a senior/junior can execute.

Remind me at the end: both CI workflows (`lint.yml` + `tests.yml`) must also be green before merge, and never push without my explicit go-ahead.
