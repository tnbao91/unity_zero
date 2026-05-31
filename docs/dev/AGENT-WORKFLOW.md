# AI Agent Workflow (repo-side)

This template was developed largely with Claude Code as a pair-programming partner. This doc records the conventions so future contributors (human or AI) can continue in the same shape.

> **Audience:** contributors developing **the package itself**.
> **Not for** consumers using the package in their game — that's `Samples~/ClaudeMemory/CLAUDE.md`.

## Agent tier ladder

The repo ships named sub-agents under `.claude/agents/`. Pick the tier that matches the task; specialists auto-trigger on diff review.

| Tier | Agent | Use for | Don't use for |
|---|---|---|---|
| Architecture | `unity-lead` (opus) | Phase planning, breaking-change decisions, alternative selection (RegisterType vs RegisterFactory, real impl vs mock), phase-close audit (JOURNAL + CLAUDE sync). | Routine feature work (delegate to senior); boilerplate (delegate to scaffolder/junior). |
| Feature implementation | `unity-senior` (sonnet) | End-to-end feature: new service following the service convention (interface → RED test → impl → installer → wire), bootstrap step, single-module refactor, docs co-located with service. Must show the RED test run before impl. | Architecture decisions (escalate to lead); pure scaffolding (delegate to scaffolder). |
| One-file scaffolding | `unity-junior` (haiku) | Lint/format fixes, NUnit test stubs, CHANGELOG entries, single-file sync from clear spec, `Mock<Name>Service` shells. | Cross-asmdef changes; new service end-to-end; architecture. |
| Specialists | `service-scaffolder` (haiku) | Scaffold a new service end-to-end from a name + summary, following the service convention exactly — including the failing EditMode test stub against the interface, named per the phase `## Spec`. Senior fills real logic until the named test goes GREEN. | Fixing existing services; designing behavior. |
| Specialists | `asmdef-boundary-reviewer` (sonnet, read-only) | Pre-merge diff review for peer-rule violations, missing transitive refs, `autoReferenced` regressions. | Editing code. |
| Specialists | `pitfalls-guard` (sonnet, read-only) | Pre-merge diff review against `docs/dev/PITFALLS.md` for re-occurring footguns. | Editing code. |

Consumer-side mirror set ships in `Packages/com.tnbao91.nobody.zero/Samples~/ClaudeMemory/.claude/agents/` — `game-lead`/`game-senior`/`game-junior` + the same three specialists tuned for consumer scope (no edits inside `Packages/com.tnbao91.nobody.zero/**`; consumer wiring goes through `ProjectScopeInstaller.UserServices.cs` partial).

## Phase + subagent pattern

Larger work is staged as **phases** per `PLAN.md` §3 and `JOURNAL.md`. The proven flow:

> **Spec before commits (SDD guides, TDD decides).** The phase's spec is a `## Spec` block at the **top of its `JOURNAL.md` entry**, written *before any implementation commit lands*. It is the **first commit on the branch** (stub entry: heading + `## Spec` only); the rest of the entry — files touched, decisions, bugs caught — fills in at phase-close (two-write cadence; resolves the JOURNAL-is-a-phase-close-artifact tension). Content of `## Spec`: (a) user-visible behavior in 1–5 bullets, (b) acceptance criteria expressed as **concrete EditMode/PlayMode test names** (`Class.Method`) that will exist on this branch. The spec guides the AI; the named tests are the executable form and the **final authority** — if they disagree, the spec is wrong (see `CLAUDE.md` anti-patterns).

0. Write the `## Spec` block as the first commit on the branch (stub `JOURNAL.md` entry: heading + `## Spec` only) — behavior bullets + acceptance criteria as concrete test names.
1. Branch `phase-<N>-<short-name>` (or `feature/<topic>` for smaller items).
2. **Spawn a junior subagent** (Haiku, `general-purpose` with `isolation: "worktree"`) to implement. The brief MUST cite (a) the `## Spec` block (which `JOURNAL.md` phase entry), (b) the exact failing test names from that spec's acceptance criteria, (c) every file path and interface to add. The subagent writes the named tests first, runs them, and **reports the RED result before writing any impl** — impl is "make these named tests GREEN", nothing more. Subagent reads `PLAN.md` + tail of `JOURNAL.md` (including the new `## Spec`) for context.
3. **Lead review in main session** (Opus). Read every file the subagent wrote, every asmdef change, every test. Patch bugs in-place — Phase 4 round 1 caught 5 production bugs this way; Phase 5a round 1 caught 11.
4. User verifies in Editor (compile clean + Test Runner green + Press Play Bootstrap.unity).
5. Complete the `JOURNAL.md` entry (the `## Spec` stub from step 0 already exists): files touched, decisions, bugs caught, verification status, resume hint.
6. Update `CLAUDE.md` if any public surface or convention changed.
7. Update `docs/dev/PITFALLS.md` if any new footgun surfaced.
8. Merge `--no-ff` to `main`.

This pattern caught ~20 production bugs before user verification across phases 4 + 5a alone. The lead-review pass is non-negotiable — junior subagents reliably miss `using` directives, asmdef registrations, and language-version constraints (Unity 6 = C# 9, no `record struct` / no `init;`).

## Source-of-truth files (read in this order before editing)

1. **`PLAN.md`** — full architectural plan, decisions, scope. ~3K tokens. Read once per session.
2. **`JOURNAL.md` tail** — last 80 lines. ~500 tokens. Tells you what shipped and any deviations.
3. **`CLAUDE.md`** — conventions + footguns + things-easy-to-miss. ~3K tokens. Reference when touching unfamiliar areas.
4. **`PITFALLS.md`** — every entry came from a real bug. Required reading before extending: pool, bootstrap pipeline, save, localization, audio, notification, input, asmdef refs, NuGet metas.

## What an AI agent should NEVER do without explicit user approval

- **Push to remote** (`git push`). User-confirmable risk.
- **Force operations** (`git push --force`, `git reset --hard`, `git rebase -i`).
- **Delete branches**, drop tags, rewrite history.
- **Modify `Packages/manifest.json` dependencies** without checking why a package is locked at a specific version (Phase 2 audio mixer compatibility, Phase 5a Reflex API).
- **Suggest substituting the locked stack** — Reflex/UniTask/R3/LitMotion. The user picked these deliberately over Zenject/Task<T>/UniRx/DOTween. Save as feedback memory if you forget.
- **Add a real third-party SDK to the template** — Mock + extension recipe pattern only. Localization and Notifications are the documented exceptions (they wrap Unity-shipped packages).
- **Touch the peer rule** — Gameplay/UI/Meta cannot reference each other. `grep "Zero.UI\|Zero.Meta" Zero.Gameplay.asmdef` must stay empty. Codex review will flag any violation.

## Slash commands

Three repeated workflows below are packaged as `.claude/commands/` so they're one-shot callable (thin wrappers that cite this doc + the agent checklists — they don't duplicate the prose):

- `/phase-open <n> <short-name>` — branch + write the `## Spec` stub as the first commit (step 0–1 below).
- `/phase-close` — run the `unity-lead` 6-item phase-close audit; verdict MERGE-READY / BLOCKED.
- `/pre-pr [base]` — fan out `asmdef-boundary-reviewer` + `pitfalls-guard` on the diff, then `/code-review`.

## Common workflows for AI-assisted contributions

### Add a new service

Follow the convention precisely (also in `CLAUDE.md` — Service convention):

1. Interface in `Packages/com.tnbao91.nobody.zero/Runtime/Core/Interfaces/I<Name>Service.cs` (`namespace Zero.Core`).
2. **EditMode test first (RED).** Write the behavior-anchored test under `Tests/EditMode/` against the interface from step 1 — happy path + edge cases, asserting *user-visible behavior* (state transitions, round-trips, documented thresholds), **not** "instantiate + assert non-null". Run it; it must fail (no impl yet). This RED run is the proof the test exercises real behavior, not a snapshot. The test names must match the phase `## Spec` acceptance criteria.
3. Impl in `Packages/com.tnbao91.nobody.zero/Runtime/Services/<Name>/` with its own asmdef — written until step 2's test goes GREEN, nothing more.
4. `<Name>ServiceInstaller` static class with single `Install(ContainerBuilder builder)` + `RegisterType` call (`Lifetime.Singleton, Resolution.Lazy`). Use `RegisterFactory` if ctor takes non-contract params (e.g., `string`, primitive — see `VersionCheckServiceInstaller`).
5. If async init needed: `<Name>Step : BootstrapStepBase` in `Runtime/Bootstrap/Steps/`.
6. Wire in `ProjectScopeInstaller.InstallBindings` — add `Install(builder)` call AND add step to `steps` array in correct position.
7. Add `docs/services/<name>.md` matching fixed format (Overview/Public API/Extension Points/Examples/Known Limitations/Design Rationale).
8. Update `CLAUDE.md` if new convention or footgun surfaced.

### Fix a bug

Reproduce → write failing test → fix → confirm test green → look at adjacent code for same-shape bugs (often footguns repeat — see Phase 4 round 2 where 3 test files all missed `using R3;`).

### Bump a dependency

Check `Packages/com.tnbao91.nobody.zero/package.json` `"dependencies"` first — that's what consumers see. Then `Packages/manifest.json` for dev workspace. Verify R3 BCL transitive deps still compatible (NuGet `packages.config` may need refresh).

## Release workflow

See `CONTRIBUTING.md` "Sample sync convention" + the release notes in `JOURNAL.md`. Summary:

```sh
# 1. Bump version in BOTH places
#    Packages/com.tnbao91.nobody.zero/package.json
#    CHANGELOG.md (root + package)
# 2. Sync Samples~ if Bootstrap.unity / 3 Resources assets / packages.config changed
# 3. Commit, tag, push
git add -A && git commit -m "release: vX.Y.Z"
git tag vX.Y.Z
git push origin main vX.Y.Z
# 4. OpenUPM bot auto-builds the new tag.
```

## Memory + context across sessions

User uses Claude Code's persistent memory. When information should outlive a session:

- **User preferences / corrections** → `feedback_*.md` memory.
- **Architectural decisions for this project** → `project_unity_zero_*.md` memory.
- **Per-phase code state** → `JOURNAL.md` (in repo, not memory).

Don't save file paths or commit SHAs to memory — those rot. Use `JOURNAL.md` for that.
