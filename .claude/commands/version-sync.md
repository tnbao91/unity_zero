---
description: Version-sync audit — after a package.json version bump, verify CHANGELOGs ×2, READMEs, JOURNAL, and tag/release all reflect the new version.
argument-hint: [expected version X.Y.Z, default = package.json]
allowed-tools: Read, Grep, Glob, Bash(git tag:*), Bash(git log:*), Bash(git diff:*), Bash(git status), Bash(git branch:*), Bash(gh release view:*), Bash(gh release list:*)
---

Run the **version-sync audit**. Read-only — do not edit; report each item PASS / FAIL / N/A with evidence (`file:line`) and a suggested one-line fix for each FAIL.

**Source of truth:** `version` in `Packages/com.tnbao91.nobody.zero/package.json` (override: `$ARGUMENTS` if given). Call it `X.Y.Z`. Previous version = the release entry directly below the top one in root `CHANGELOG.md`.

1. **Root `CHANGELOG.md`** — has a `## [X.Y.Z] — YYYY-MM-DD` section, and the `## [Unreleased]` section above it is empty (all deltas moved into the release entry).
2. **Package `CHANGELOG.md`** (`Packages/com.tnbao91.nobody.zero/CHANGELOG.md`) — has its own `## [X.Y.Z]` entry with the same date. Content is consumer-tuned by design — check presence + version + date only, never byte-diff against root.
3. **Root `README.md`** — any "Latest is `…`" / version-pin guidance names `X.Y.Z`. (The "minimum `0.2.1`, tags `v0.1.0`/`v0.2.0` broken" warning is historical and stays as-is — don't flag.)
4. **Package `README.md`** — same check; today it intentionally states only the minimum version, so flag only a stale "latest" claim.
5. **`docs/dev/JOURNAL.md`** — the newest phase entry references `vX.Y.Z` (phase workflow: JOURNAL updated before merge).
6. **Stale-version sweep** — grep the repo (skip `Library/`, `.git/`, and the history-by-design files: both CHANGELOGs, JOURNAL) for the previous version string; judge each hit in context — historical references are fine, "current/latest" claims and install-pin examples are drift.
7. **Tag + release** — if the bump commit is already on `main`: `git tag --list vX.Y.Z` and `gh release view vX.Y.Z` must both exist (project convention: every version-bump PR gets a matching tag + GitHub release after merge). If the bump is still on an unmerged branch, report this item as **REMINDER** (create after merge), not FAIL.

End with a one-line verdict: **SYNCED** or **DRIFT in items [n…]**. This deepens `/phase-close` item 5 (which only checks that a bump happened) — run it after a bump lands or as part of cutting a release.
