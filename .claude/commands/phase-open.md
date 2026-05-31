---
description: Open a new phase — branch + write the ## Spec stub as the first commit (SDD guides, TDD decides).
argument-hint: <phase-number> <short-name>
allowed-tools: Read, Write, Edit, Bash(git branch:*), Bash(git checkout:*), Bash(git add:*), Bash(git commit:*), Bash(git status), Bash(git log:*)
---

Open phase **$ARGUMENTS** following `docs/dev/AGENT-WORKFLOW.md` §"Phase + subagent pattern", step 0–1. Do exactly this, nothing more — implementation comes later:

1. Read the tail of `docs/dev/JOURNAL.md` and `docs/dev/PLAN.md` §3 to place this phase and get its scope.
2. Create the branch: `phase-<number>-<short-name>` (or confirm with me if a `feature/<topic>` branch fits better for a small item).
3. Add a **stub** `JOURNAL.md` entry — heading + a `## Spec` block ONLY (do not fill files-touched / decisions yet; those land at phase-close, the two-write cadence):
   - **(a)** user-visible behavior in 1–5 bullets.
   - **(b)** acceptance criteria as **concrete EditMode/PlayMode test names** (`Class.Method`) that will exist on this branch. These named tests are the executable spec and the final authority — if prose and tests later disagree, the prose is wrong.
4. Commit the stub as the **first commit on the branch** (`docs(journal): phase <n> spec stub`).

Stop after the commit. Report the branch name + the test names you committed so the implementation step can target them RED-first.
