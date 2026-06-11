# CLAUDE.md

Guidance for Claude Code (claude.ai/code) when working in this repo.

> **This file is a constitution.** Only principles + anti-patterns + references. Module details live in `docs/`; footgun cheatsheet lives in `docs/dev/PITFALLS.md`. Before adding a sentence here, ask: *"Is this an invariant principle, or a module detail?"* If detail, put it in the right doc and link.

## Project

Unity 6 LTS (`6000.3.11f1`) greenfield template for hybrid casual games. Single bootstrap scene at `Assets/_Project/Scenes/Bootstrap.unity`. URP, new Input System, Addressables, IAP, NuGetForUnity. Runtime code lives in `Packages/com.tnbao91.nobody.zero/Runtime/` (the package); `Assets/_Project/` is the consumer-side host project.

## Stack (locked — do not substitute)

| Concern | Choice | Not |
|---|---|---|
| DI | Reflex | Zenject, VContainer |
| Async | UniTask | `Task<T>` for game code |
| Reactive | R3 | UniRx |
| Tweening | LitMotion | DOTween |
| JSON | Newtonsoft.Json (NuGetForUnity) | — |
| Strings | ZString | — |

Substitution suggestions are rejected on sight. The user picked these deliberately.

## Core principles

- **Service convention.** Every service: interface in `Runtime/Core/Interfaces/I<Name>Service.cs` (namespace `Zero.Core`) → **failing behavior-anchored EditMode test against that interface (RED before impl)** → sealed impl in `Runtime/Services/<Name>/` with own `Zero.Services.<Name>.asmdef` → `<Name>ServiceInstaller` static class → optional `<Name>Step : BootstrapStepBase` → wire into `ProjectScopeInstaller.InstallBindings` (call `Install` + add step to `steps[]` in correct position). Reference impls: `LocalizationServiceInstaller`, `VersionCheckServiceInstaller` (the latter shows `RegisterFactory` for ctors with primitives). Full ordered steps: `docs/dev/AGENT-WORKFLOW.md` §"Add a new service".
- **Asmdef boundaries.** `Zero.Core` holds interfaces + POCOs only; never references service impls. `Zero.Gameplay`, `Zero.Meta`, `Zero.UI` are **peers** — they never reference each other. Cross-tier coupling goes through `IEventBus` only.
- **Sealed services + interface seams.** Every impl is `sealed`. Extension is by swapping the binding (in `<Name>ServiceInstaller`, or consumer-side by re-registering the contract in an own `ContainerScope.OnRootContainerBuilding` installer — last registration wins) or by decorator-wrapping. Never document "subclass and override", and never document a cross-assembly `partial` seam (invalid C# — see PITFALLS).
- **Mock-first defaults.** Third-party SDK integrations ship as `Mock<Name>Service`. Real adapters replace the binding per-game. Real impls already in the template wrap Unity-shipped packages (Localization, Mobile Notifications, ObjectPool, Audio Mixer, IAP). Details + key conventions per service in `docs/services/<name>.md`.
- **Gameplay is genre-agnostic.** `Zero.Gameplay` ships only state-machine + level-loading scaffolds + lifecycle events. Grid/runner/idle/merge/match-3 systems are out of scope — they live in the consumer's game asmdef.
- **Consumer owns scenes and UI roots.** Only `Bootstrap.unity` is in build settings. UI layer canvases are not spawned by the framework — consumer attaches a `UIRoot` MonoBehaviour with 4 Transform slots. Loading screens, popups, screens, toasts are loaded by Addressables key conventions (`ui/popup/<name>`, etc.) provided by the consumer.
- **Validate inputs at service boundaries.** Every public service method guards caller-supplied input (keys, ids, indices, durations) before use: write/action methods throw on invalid input, query methods fail safe (return `false`/empty/no-op). Precedents: `UnityPoolService.PrewarmAsync` (throws), `EncryptedJsonSaveService.TryGet` + `UnityLocalizationService.Get` (fail safe). Mechanics + the Addressables-key footgun: `docs/dev/PITFALLS.md` → "Validate inputs at service boundaries" and "Addressables logs the red exception BEFORE your try/catch sees it".

## Testing principles

- **Behavior-anchored, not snapshot.** A test that asserts a consumer-facing behavior (`IBootstrapProgressReporter` emits progress in the right order when a step retries; gestures fire at the documented thresholds; save round-trips through encryption + tamper-reset) earns its weight. A test that instantiates a MonoBehaviour and asserts non-null does not.
- **Coverage philosophy.** Coverage thresholds, if any, are enforced in CI (`.github/workflows/tests.yml`), never in the local dev gate. Don't optimize the coverage number; optimize for *"would this test catch a real regression?"*. 100% from snapshot-style tests is worthless; 60% from behavior-anchored tests is solid.
- **Async EditMode test pattern.** `[UnityTest] public IEnumerator Foo() => UniTask.ToCoroutine(async () => { ... });`. NUnit's `[Test]` does not await `UniTask` — assertions run before the body completes. Pure-sync tests may keep `[Test]`.

## Debug philosophy

- **Phenomenon → hypothesis → test → fix.** When chasing a bug: state the observed phenomenon, form a hypothesis, write an EditMode/PlayMode test that *would fail iff the hypothesis is true*, confirm it fails, then fix until it passes. A green test without a prior red is not evidence.
- **Git history is memory.** Before fixing, `git log --grep="<keyword>"` and look at the diff for similar fixes. Many bugs are regressions of past fixes — don't re-pay the cost.
- **Same-shape sweep after a fix.** When you find one occurrence of a footgun, grep for the rest. Phase 4 round 2 found three test files all missing `using R3;` together.

## Honesty & uncertainty

- **Only act at 100% certainty; otherwise ask.** When the request, the code, or a tool result is ambiguous, ask the user instead of guessing. A confident-looking wrong guess costs more than a question.
- **Verify before you claim.** Never assert a `file:line`, a symbol, or a behavior you have not actually read *this session*. A cancelled or unrendered tool call is not evidence — re-read before relying on it. If a check could not complete, say so explicitly rather than presenting an assumption as a finding.
- **Report outcomes faithfully.** Claude cannot open Unity (see *Build & test*) — when a change needs Editor verification, say so; never claim tests pass that you did not run.

## Automated gate before PR

- `.github/workflows/lint.yml` (CI) + `.pre-commit-config.yaml` (local) — formatting, EOL, basic static checks.
- `.github/workflows/tests.yml` (CI) — Unity Test Runner EditMode + PlayMode headless via `game-ci`.
- Both must be green before merge. Never bypass `--no-verify` / `--no-gpg-sign` unless the user explicitly asks.
- AI-side review: spawn `asmdef-boundary-reviewer` + `pitfalls-guard` on the diff before opening the PR.
- **Harness guardrails (inline, during editing).** Footguns are enforced at the least-powerful tool that can decide them — one catalog (`docs/dev/PITFALLS.md` → "Enforcement surface"), three surfaces: **permission** (path rules in `.claude/settings.json` + consumer `settings.example.json` block edits inside the package / `ask` on deps + version files), **hook** (`.claude/hooks/check-footguns.sh`, warn-only `PostToolUse` catching the context-free subset — legacy `Input.*`, `dynamic`, C# 9 violations, `using R3;`), **agent** (the two reviewers above, for judgment checks). The hook nudges; the agents + CI are the hard gate.

## Anti-patterns (do not)

- Cross-reference `Zero.Gameplay` ↔ `Zero.Meta` ↔ `Zero.UI`. Use `IEventBus`.
- Add genre-specific systems into `Zero.Gameplay`.
- Add a real third-party SDK to the template. Mocks only (exception: Unity-shipped packages, already wired).
- Use legacy `Input.*` API (`Input.touchCount`, `Input.GetKey`, `Input.mousePosition`...). Active Input Handling is "Input System Package" — legacy calls throw at runtime.
- `RegisterType` for a ctor that takes a primitive or any unbound type. Use `RegisterFactory`.
- Call `Object.Destroy` or `Object.DontDestroyOnLoad` without an `Application.isPlaying` guard (or use `UnityPoolService.SafeDestroy`). EditMode tests will throw.
- Edit a test to make it agree with the spec when the two disagree. Tests are the executable spec and the final authority — fix the prose spec (the `## Spec` block in `JOURNAL.md`), never weaken the test to match it.
- Use `dynamic` in Runtime code. IL2CPP/AOT does not support the DLR.
- Subscribe to R3 streams via lambda without `using R3;` at the top of the file. The lambda will bind to the wrong overload (CS1660).
- Use C# 10+ syntax (`record struct`, `init;`, `required`, file-scoped namespaces). Unity 6 = C# 9.
- Assert a `file:line`, symbol, or behavior you have not read this session, or claim Editor-only tests passed without running them. Verify before claiming; ask when unsure.
- Accept caller-supplied keys/ids/indices into a public service method without a guard (null/empty/range).
- Allocate per-frame in `Update`/`LateUpdate`/`OnGUI` (`new` array/list, LINQ, string interpolation). Reuse buffers; build strings only when the value changes.

## Build & test

This is a Unity project — no shell-level build script. Verify changes inside the Editor:

- **Open**: Unity 6.0.3.11f1 (see `ProjectSettings/ProjectVersion.txt`).
- **Play**: `Assets/_Project/Scenes/Bootstrap.unity` → Play. `[Bootstrap] Step N/M: ...` lines appear in Console.
- **Tests**: `Window → General → Test Runner` (EditMode + PlayMode).
- **Headless** (Editor must be closed first):
  `Unity -batchmode -nographics -projectPath . -runTests -testPlatform editmode -testResults results.xml -quit`

Claude cannot open Unity. When a change requires Editor verification, say so explicitly rather than claiming it works.

## References

Read in this order at the start of a session:

- `docs/dev/PLAN.md` — full architectural plan + phase scope.
- `docs/dev/JOURNAL.md` (tail) — what shipped per phase + decisions.
- `docs/dev/PITFALLS.md` — **required** before extending: pool, bootstrap pipeline, save, localization, audio, notification, input, asmdef refs, NuGet metas, UI raycasting, IL2CPP/AOT. Every entry came from a real bug.
- `docs/dev/AGENT-WORKFLOW.md` — phase + subagent pattern, source-of-truth file order, agent tier ladder.

Module detail:

- `docs/architecture/{asmdef-graph,bootstrap-pipeline,event-bus}.md`
- `docs/services/<name>.md` — one per service, format: Overview / Public API / Extension Points / Examples / Known Limitations / Design Rationale.
- `docs/ui/*` — `ui-root.md`, `popup-stack.md`, `toast.md`, `loading-screen.md`, `localized-text.md`, `safe-area.md`.
- `docs/gameplay/{state-machine,level-loading}.md`.
- `docs/security/save-encryption.md`.
- `docs/testing/{writing-tests,ci,manual-checklist}.md`.

AI sub-agents (two parallel sets — pick the right tier; specialists auto-trigger on diff review):

- Maintainer-side at `.claude/agents/`: `unity-lead` (architecture/breaking) → `unity-senior` (feature implementation) → `unity-junior` (one-file scaffolding/lint). Specialists: `service-scaffolder`, `asmdef-boundary-reviewer`, `pitfalls-guard`.
- Consumer-side at `Packages/com.tnbao91.nobody.zero/Samples~/ClaudeMemory/.claude/agents/`: `game-lead`/`game-senior`/`game-junior` + the same three specialists tuned for consumer scope (no edits inside `Packages/com.tnbao91.nobody.zero/**`; consumer-side wiring goes in the consumer's own `OnRootContainerBuilding` installer + `BootstrapStepRegistration`).
