---
description: Phase-close audit — run the unity-lead 6-item checklist before approving a merge to main.
allowed-tools: Read, Grep, Glob, Bash(git diff:*), Bash(git log:*), Bash(git status), Bash(git branch:*)
---

Run the **phase-close audit** from `.claude/agents/unity-lead.md` → "Phase-close checklist". This is a read-only audit — do not edit; report each item PASS/FAIL with the evidence, and block the merge if any fails. Walk the 6 items against the current branch diff (`git diff main`):

1. Branch builds + Test Runner green in Editor (I verify via Unity MCP — ask me for the result; do not claim it yourself).
2. `docs/dev/JOURNAL.md` has the full entry for this phase: files touched, key decisions, bugs caught in review, verification status, resume hint (the `## Spec` stub from `/phase-open` should now be completed).
3. `CLAUDE.md` updated if any public surface, asmdef, or convention changed.
4. `docs/dev/PITFALLS.md` updated if any new footgun surfaced this phase — and tagged with its enforcement surface (`[permission | hook | agent]`, see the "Enforcement surface" legend).
5. Both `package.json` and root `CHANGELOG.md` bumped if this phase ships externally.
6. `Samples~` synced if `Bootstrap.unity`, the 3 Resources assets, or `packages.config` changed.

End with a one-line verdict: **MERGE-READY** or **BLOCKED on items [n…]**.
