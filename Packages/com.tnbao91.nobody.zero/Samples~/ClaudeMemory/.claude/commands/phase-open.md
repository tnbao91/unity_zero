---
description: Open a feature — branch + name the behavior-anchored tests you'll write RED-first (your executable spec).
argument-hint: <short-topic>
allowed-tools: Read, Write, Edit, Bash(git branch:*), Bash(git checkout:*), Bash(git status), Bash(git log:*)
---

Open feature **$ARGUMENTS** on a game built on the Zero template. Consumers don't keep an upstream JOURNAL/phase ledger — **your named tests are the executable spec and the final authority** (CLAUDE.md anti-pattern: never weaken a test to match prose; fix the prose). Do exactly this — implementation comes later:

1. Read `CLAUDE.md` + `claude-context/architecture.md` to place the work. Decide which of YOUR game asmdefs it touches (`Game.Gameplay` / `Game.UI` / `Game.Meta` or your equivalents) and confirm the peer rule holds — those asmdefs never reference each other; cross-tier goes through `IEventBus`.
2. Branch `feature/<topic>`.
3. Decide acceptance criteria as **concrete EditMode/PlayMode test names** (`Class.Method`) in YOUR test asmdef — behavior-anchored (state-machine transition order, save round-trip, gesture at documented thresholds), **not** instantiate-and-null-check.
4. Report the branch name + the test names. The implementation step (`game-senior`) writes those tests first, runs them **RED**, then writes impl only until they go GREEN — nothing more.

Stop after reporting. Do not implement now.
