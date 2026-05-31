---
description: Pre-merge audit for a game feature — the game-lead checklist before merging to your main.
allowed-tools: Read, Grep, Glob, Bash(git diff:*), Bash(git log:*), Bash(git status), Bash(git branch:*)
---

Run a read-only **pre-merge audit** in the spirit of `.claude/agents/game-lead.md`. Do not edit — report each item PASS/FAIL with evidence and block the merge if any fails. Walk against the current diff (`git diff <your-main-branch>`):

1. **Peer rule holds in YOUR asmdefs** — `Game.Gameplay` / `Game.UI` / `Game.Meta` (or your equivalents) do not reference each other; cross-tier coupling is `IEventBus` only. `grep "Game.UI\|Game.Meta"` the Gameplay asmdef etc. — must be empty across tiers.
2. **No edits inside the package** — nothing under `Packages/com.tnbao91.nobody.zero/**` changed. Real-SDK swaps + new services went through a binding swap / `ProjectScopeInstaller.UserServices.cs` partial, never a fork. (See `claude-context/extension-points.md`.)
3. **Asmdef discipline** — every new asmdef has `autoReferenced: false`, and no game asmdef references `Zero.Bootstrap` (the composition root — game code references services + peers, not the wirer).
4. **Tests behavior-anchored & green** — Test Runner via Unity MCP (ask me for the result; do not claim it yourself). The RED-first tests from `/phase-open` are now GREEN.
5. **Pitfalls self-check** vs `claude-context/pitfalls.md` — UI raycasting, save/encryption, audio, notifications, input, Addressables `HasKeyAsync`, Reflex DI.
6. **Secrets + commits** — `ZeroSecrets.asset` configured if this touches save/encryption; conventional-commit messages (`feat(shop): …`).

End with a one-line verdict: **MERGE-READY** or **BLOCKED on items [n…]**.
