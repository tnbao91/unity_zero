# Changelog

All notable template-level changes are recorded here. Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) loosely; per-phase deltas with file-level detail and bug retros live in [`docs/dev/JOURNAL.md`](docs/dev/JOURNAL.md).

## [Unreleased]

## [0.5.1] — 2026-07-02 — Full-project review fixes

### Fixed
- `DefaultAdPlacementService` boundary guards: null/empty placement ids crashed queries; `RegisterPlacement` accepted invalid caps/cooldowns. First EditMode test suite for the service (15 methods).
- Constitution/doc drift: `CLAUDE.md` stale Unity version (`6.0.3.11f1` → `6000.5.0f1`), CI description (EditMode-only, PlayMode placeholder is deliberate), `UnityPoolService.SafeDestroy` → `Zero.Infrastructure.Util.SafeDestroy`; `docs/architecture/asmdef-graph.md` re-synced with actual asmdef references (post-v0.3.0 R3 sweep, pipeline location, Events-ref count, verify-grep path).

### Changed
- CI: workflows no longer double-run on PR pushes (`push` limited to `main`) and superseded runs are cancelled via a concurrency group.

## [0.5.0] — 2026-06-20 — Unity 6.5 upgrade

### Changed
- **Minimum Unity version: 6000.3 → 6000.5 (`6000.5.0f1`).** Consumers on Unity 6.3 must stay on 0.4.x.
- **`UnityPoolService`: `GetInstanceID()` → `GetEntityId()`.** Unity 6.5 deprecates `Object.GetInstanceID()` (CS0619) in favour of the 64-bit `EntityId` struct. Internal pool dictionaries split into `_goPools: Dictionary<EntityId, GameObjectPool>` and `_wrapPools: Dictionary<(EntityId, Type), IPoolHandle>`, eliminating the fragile int-XOR key for component wrappers. No change to the public `IPoolService` API.

### Package bumps (auto-resolved by Unity 6.5 Package Manager)
| Package | Old | New |
|---|---|---|
| LitMotion | 2.0.1 | 2.0.2 |
| R3 | 1.3.0 | 1.3.1 |
| UniTask | 2.5.10 | 2.5.11 |
| Reflex | 14.3.0 | 14.3.1 |
| Unity Localization | 1.5.11 | 1.5.12 |
| Unity Purchasing/IAP | 5.2.1 | 5.3.1 |
| URP | 17.3.0 | 17.5.0 |
| Test Framework | 1.6.0 | 1.7.0 |
| uGUI | 2.0.0 | 2.5.0 |

## [0.4.0] — 2026-06-11 — Production hardening (Phase 6)

Hardening pass driven by a full-framework review (3 audit agents + line-level manual verification + design review). Minor bump: additive public API (bootstrap failure events + consumer step seam). Two deliberate behavior changes are called out under *Changed*.

### Added
- **Bootstrap failure seam.** A critical step failure/timeout now publishes `BootstrapFailed { StepName, Error, Attempt }` on `IEventBus` and surfaces as `BootstrapStepFailedException` (inner = original failure). `GameLauncher` subscribes to `BootstrapRetryRequested` and re-runs the pipeline, so a consumer loading screen can own retry UX — previously the launcher logged and the player hung on a dead splash forever. Steps must be idempotent across re-runs (new PITFALLS entry).
- **Consumer bootstrap-step seam.** Register `BootstrapStepRegistration` (`Append`/`Before`/`After`/`Replace` + anchor step name) from your own `OnRootContainerBuilding` installer; the pipeline factory composes registrations onto the 16 defaults in registration order (`BootstrapStepComposer`). Typo'd anchor names throw at boot instead of silently skipping. Reflex's `All<T>()` registration-order behavior is pinned by a dedicated test.
- `link.xml` template in `Samples~/BootstrapScene` — IL2CPP strips save-model types deserialized through Newtonsoft reflection; fails on device only.
- PITFALLS entries: swap-real-SDK criticality/timeout review, bootstrap step idempotency, cross-assembly `partial` invalidity, IL2CPP save-model stripping. Plus a per-step criticality/timeout defaults table in `docs/architecture/bootstrap-pipeline.md`.

### Changed
- **`CrashlyticsStep`: `IsCritical` → `false`, `Timeout` → 5s** (was critical + 30s default). Ordering ≠ criticality — the step stays first so later failures get reported, but a crash-reporter outage can no longer block app launch (aborting produces zero reports anyway). Invisible with the shipped mock; semantic once a real SDK is swapped in — flipping pre-1.0 is the cheapest this will ever be.
- **`ProjectScopeInstaller.Hook` moved to `BeforeSplashScreen`** (was `BeforeSceneLoad`) so template bindings always register before consumer installers. Consumer re-registration of a contract (last-write-wins) is now deterministic instead of depending on Unity's unspecified cross-assembly `RuntimeInitializeOnLoadMethod` ordering.
- **Extension story corrected everywhere.** "Extend `ProjectScopeInstaller` via a `UserServices.cs` partial" is invalid C# for UPM consumers — partials cannot span assemblies. Both CLAUDE.md files, `extension-points.md`, and the installer's own comments now teach the real seams: `OnRootContainerBuilding` (bindings) + `BootstrapStepRegistration` (steps); the partial remains documented as fork-mode-only. `extension-points.md` recipes also fixed against the actual interfaces: nonexistent `ISaveService.Get<T>(key, default)` (×3), wrong `ICrashlyticsService` members, false "auto-saves on app pause" claim, subclass-the-sealed-class `Migrate` advice, and `[DefaultExecutionOrder]` ordering advice that has no effect on load methods.
- `docs/services/save.md` gains the corrupt-quarantine and **mobile `OnApplicationPause(true) → SaveAsync` recipe (required for production mobile)**; `docs/security/save-encryption.md` gains the IL2CPP stripping practice.

### Fixed
- **Save data-loss paths.** Corrupt/tampered save files are quarantined to `save.dat.corrupt` before reset-to-empty (recovery/forensics seam — previously the next save overwrote the only copy). `Dispose()` now synchronously flushes a `RequestSave` still inside the 1s debounce window (previously cancelled and silently dropped); flush uses a dedicated sync write core because blocking on `SaveAsync` would deadlock on its main-thread continuation.
- **Popup bookkeeping mis-pop.** A cancelled `PushAsync` blindly popped whichever popup was on top of `_activePopups` — with interleaved pushes, cancelling the first evicted the second's entry and the next `PopAsync` closed (and published `PopupClosed` for) the wrong popup. Same-shape sweep fixed the identical pattern in `PopupStack` (sort-order entries) and the close path. Both structures now remove by reference; routine `OperationCanceledException` paths no longer log an error.
- Boundary guards per the validate-at-boundaries principle: `LogService.Error(null)` no-ops safely (context still logged), empty-text toasts are ignored with a warning, zero/negative toast durations clamp to 0.5s.

### Tests
- EditMode suite 82 → 101 methods. New: bootstrap failure event/exception contract (×3), Crashlytics non-criticality (×2), save quarantine + dispose flush (×2), log/toast guards (×4), step composer anchors (×6), Reflex registration-order pin, popup interleaved push/cancel. The two pre-existing critical-abort tests now assert the wrapped `BootstrapStepFailedException` per the Phase 6 spec (recorded in JOURNAL).

## [0.3.0] — 2026-05-31 — AI agent harness guardrails

### Added
- **AI agent harness guardrails** (repo-side dev tooling). Footgun rules are now enforced at the least-powerful tool that can decide them — one catalog (`docs/dev/PITFALLS.md` → new "Enforcement surface" legend), three surfaces: **permission** (`.claude/settings.json` read-only allow-list + `ask` on dep/version files), **hook** (`.claude/hooks/check-footguns.sh`, warn-only `PostToolUse` catching the context-free subset — legacy `Input.*`, `dynamic`, C# 10 syntax, `Subscribe` without `using R3;`), **agent** (`pitfalls-guard` / `asmdef-boundary-reviewer` for judgment checks).
- Three repo-side slash commands packaging existing workflows: `/phase-open`, `/phase-close`, `/pre-pr`.
- Consumer sample (`Samples~/ClaudeMemory`): mirrored game-tuned `/phase-open`, `/phase-close`, `/pre-pr`, plus a `settings.example.json` `deny` rule that blocks edits inside the package. **Re-import the sample to get them.**

### Changed
- De-duplicated the EditMode-safe destroy guard: `Zero.UI.UiObjects.SafeDestroy` (internal) and `UnityPoolService.SafeDestroy` (private) were byte-identical. Consolidated into one `public static Zero.Infrastructure.Util.SafeDestroy(GameObject)` — both `Zero.UI` and `Zero.Services.Pool` already reference `Zero.Infrastructure`, so no asmdef-boundary or peer-rule violation (the original "duplicated to avoid a cross-asmdef ref into `Zero.Services.Pool`" rationale overlooked `Zero.Infrastructure` as a shared home). `UiObjects` removed. No behavior change. The two consolidated symbols were not consumer-visible (`internal` / `private`); the replacement `Zero.Infrastructure.Util.SafeDestroy` is intentionally `public` — a new (small) consumer-facing helper, and the designated home for further cross-cutting helpers.

## [0.2.3] — 2026-05-17 — Post-review cleanup

Post-review pass (asmdef + pitfalls + architecture). No behavior change to shipped services; scope tightening + EditMode-safety hardening.

### Removed
- Example state shells (`BootState`, `MenuState`, `PlayState`, `PauseState`, `ResultState`) from `Zero.Gameplay`. Gameplay states are consumer-authored — sample shells in shipped `Runtime/` blurred the genre-agnostic boundary. The `GameStateMachine` + `LevelLoader` scaffold and lifecycle events are unaffected. Consumer-visible API removal → patch bump.

### Fixed
- `Zero.UI` (`UIService`, `ScreenManager`, `ToastQueue`) and `AudioMixerService` no longer call `Object.Destroy` / `await UniTask.Yield()` from a path reachable in EditMode without an `Application.isPlaying` guard. Added `Zero.UI.UiObjects.SafeDestroy` (mirrors `UnityPoolService.SafeDestroy`).
- `AudioMixerService.PlayMusicAsync` / `PlaySfxAsync` now `HasKeyAsync` pre-check before `LoadAsync<AudioClip>` — a missing clip key no longer logs a red `InvalidKeyException` outside the try/catch.

### Changed
- Service docs (`localization`, `pool`, `audio`, `version-check`) no longer say "subclass / override / extend" on `sealed` impls or `readonly struct`s — corrected to the binding-swap / decorator extension model per the sealed-services principle in `CLAUDE.md`.
- Removed inert `"R3"` entry from `references[]` in 18 runtime asmdefs (R3 ships as a NuGetForUnity DLL with `overrideReferences:false`; the asmdef entry was silently ignored).

## [0.2.2] — 2026-05-12 — Docs: CLAUDE.md as constitution

Repo-wide documentation pass. Re-anchors `CLAUDE.md` as a constitution (principles + anti-patterns + references) rather than an index. No runtime / package API changes — safe drop-in.

### Changed
- `CLAUDE.md` rewritten (120 → 97 lines): drops the "Things that are easy to miss" cheatsheet and per-service "Mock-first defaults" details — those live in `docs/dev/PITFALLS.md` and `docs/services/<name>.md` already. Adds testing philosophy (behavior-anchored over snapshot, coverage thresholds CI-only), debug philosophy (phenomenon → hypothesis → test → fix; `git log --grep` before re-fixing), and the automated-gate references (`.github/workflows/lint.yml` + `tests.yml` + `.pre-commit-config.yaml`).
- `Packages/com.tnbao91.nobody.zero/Samples~/ClaudeMemory/CLAUDE.md` slimmed from 113 → 55 lines on the same principle — cheatsheets pushed to `claude-context/*.md`.
- `docs/architecture/bootstrap-pipeline.md` adds an explicit "How the root container is built" section (ReflexSettings.asset is intentionally empty; wiring is in code via `OnRootContainerBuilding`).
- `docs/gameplay/state-machine.md` Design Rationale adds the "why genre-agnostic" rule for `Zero.Gameplay`.
- `docs/dev/AGENT-WORKFLOW.md` gains an explicit Agent tier ladder table (lead / senior / junior / specialists) for both maintainer and consumer agent sets.
- `.claude/agents/{unity-lead,pitfalls-guard,asmdef-boundary-reviewer}.md` + the consumer mirror under `Samples~/ClaudeMemory/.claude/agents/` clarify source-of-truth (`CLAUDE.md` + `PITFALLS.md`) vs operational catalog.
- `README.md` description of `CLAUDE.md` updated to match the new framing.

### Removed
- `README.vi.md`. English-only going forward — the Vietnamese pitch is no longer maintained.

### Notes
- Package `0.2.2` ships the consumer-side CLAUDE.md slim; rest is repo-side (contributor surface only).

## [0.2.1] — 2026-05-10 — Hotfix: restore Log/ asmdef

CI on case-sensitive Linux exposed that `git mv` during the v0.1.0 UPM restructure silently dropped 6 files in `Runtime/Services/Log/` (LogService, LogServiceInstaller, asmdef + metas). Cause: dev machine's global `~/.gitignore_global` had a lowercase `log` pattern that matched the `Log/` folder name on case-insensitive macOS. Local Library/ScriptAssemblies cache hid the breakage; clean Linux clone exposed it.

### Fixed
- Restored 6 missing `Runtime/Services/Log/` files. `Zero.Services.Log` namespace + asmdef are back; `ProjectScopeInstaller.cs` line 20 (`using Zero.Services.Log;`) compiles again.
- `.gitignore` adds negation `!Packages/**/Services/Log/**` to prevent recurrence — same class of fix as the earlier `Documentation~`/`Samples~` negations for the `*~` pattern.

### Note
- v0.1.0 and v0.2.0 tags ship with the broken state and should NOT be installed. v0.2.1 is the first installable release.

## [0.2.0] — 2026-05-10 — AI agent memory bundle

Adds opt-in Sample bundle so Claude Code (and similar AI coding agents) can be productive in consumer projects without re-deriving conventions every session.

### Added
- `Samples~/ClaudeMemory/` package sample with:
  - `CLAUDE.md` consumer-voice (extends package, doesn't modify it).
  - `claude-context/architecture.md` — asmdef tier diagram, peer rule, DI flow, bootstrap order.
  - `claude-context/available-services.md` — all ~28 service interfaces + signatures cheatsheet.
  - `claude-context/extension-points.md` — 8 concrete recipes (swap mock SDK, add state, popup, cheat command, bootstrap step, persist data, custom service, lifecycle subscriptions).
  - `claude-context/stack-constraints.md` — locked picks (Reflex/UniTask/R3/LitMotion/etc.) + pushback rules when AI suggests substitutes.
  - `claude-context/pitfalls.md` — consumer-relevant subset of upstream `docs/dev/PITFALLS.md`.
  - `.claude/settings.example.json` — permission skeleton for fewer prompts.
  - `README.md` — usage flow for the sample.
- `docs/dev/AGENT-WORKFLOW.md` — repo-side, documents the phase + subagent + lead-review pattern used to build the template.
- `package.json` `samples` array gains the `ClaudeMemory` entry.
- README sections (root + package) advertise the AI agent compatibility.

### Notes
- Claude-only scope for v0.2.0. Cross-tool support (`AGENTS.md`, Cursor rules, Copilot instructions) deferred until requested.
- Two distinct AI surfaces: **repo-side** (`CLAUDE.md` + `docs/dev/`) for contributors developing the package, **consumer-side** (`Samples~/ClaudeMemory/`) for studios using the package in their game.

## [0.1.0] — 2026-05-10 — UPM package release

Restructured from full-project template to embedded-package layout. Same v0 template features; new distribution mechanism.

### Added
- `Packages/com.tnbao91.nobody.zero/` — UPM package containing all Runtime + Tests + a `Samples~/BootstrapScene` bundle (Bootstrap.unity, ZeroSecrets.asset.example, ReflexSettings.asset, packages.config).
- Repo serves both as embedded-package dev workspace AND consumer-installable package source.
- `package.json` declares 11 OpenUPM/Unity dependencies.
- README rewritten with two install paths: UPM-via-git-URL for consumers, clone-dev-workspace for contributors.

### Changed
- `Assets/_Project/Scripts/Runtime/` → `Packages/com.tnbao91.nobody.zero/Runtime/` (~29 asmdefs, all `.cs.meta` GUIDs preserved via `git mv`).
- `Assets/_Project/Scripts/Tests/` → `Packages/com.tnbao91.nobody.zero/Tests/`.
- `Assets/_Project/Scripts/Editor/` → `Packages/com.tnbao91.nobody.zero/Editor/` (empty asmdef placeholder).
- Dev `Packages/manifest.json` adds `"testables": ["com.tnbao91.nobody.zero"]` so Test Runner picks up package tests in dev. Consumer installs do NOT inherit this — tests stay hidden in consumer's Editor.

### Notes
- BCL transitive deps for R3 (`Microsoft.Bcl.AsyncInterfaces`, `Microsoft.Bcl.TimeProvider`, `System.ComponentModel.Annotations`, `System.Threading.Channels`) require NuGetForUnity prereq; not bundled to avoid duplicate-DLL conflicts when consumer follows R3's OpenUPM README.
- `Documentation~/` ships only an index pointing to GitHub; full docs live in `docs/` at repo root.

## [Unreleased] — v0 template

This is the initial template release. There is no v1 yet — consumers fork and build their game from this baseline.

### Phase 1a / 1b — Foundation

- Asmdef restructure to peer-layer DAG (Gameplay / Meta / UI as peers, talking via `IEventBus`).
- New `Zero.Services.Events` with `R3EventBus` (type-keyed `Subject<T>`, no boxing of value-type events).
- New `Zero.Services.Localization` wrapping `com.unity.localization` (no custom string-table impl).
- `IBootstrapStep` gains `Timeout` (default 30s) + `MaxRetries` (default 1). `BootstrapPipeline` slices per-step progress and writes to `IBootstrapProgressReporter` (Singleton in `Zero.Infrastructure`); the loading screen reads from the reporter, not the pipeline (avoids Lazy-singleton resolution race).
- `EncryptedJsonSaveService` reads encryption seeds from `Resources/ZeroSecrets.asset` (gitignored, `.example` template ships). Player builds throw on missing/placeholder; Editor builds warn but continue.
- `ReflexPoolService` renamed to `UnityPoolService` and rewrapped on top of `UnityEngine.Pool.ObjectPool` to inherit `collectionCheck` + `maxSize`.
- EditMode tests + GitHub Actions CI (`game-ci/unity-test-runner@v4`, EditMode only on `6000.3.11f1`).
- Docs: architecture, save, localization, pool, security, testing, CI.

### Phase 2 — Real Input + Audio + Notification

- `UnityInputService` (`Zero.Services.Input`) wraps Unity Input System + EnhancedTouch. Internal `InputDriver` MonoBehaviour polls per-frame; gestures: tap (<200ms + <20px), swipe (≥50px in <500ms), drag, two-finger pinch.
- `AudioMixerService` (`Zero.Services.Audio`) loads `audio/main_mixer` via `IAssetService.HasKeyAsync` pre-check (no red-error on fresh clone). Buses Master/Music/Sfx/Ui/Voice persisted via `ISaveService`. Music crossfade via LitMotion; SFX through pooled AudioSource template.
- `UnityMobileNotificationService` (`Zero.Services.Notification`) wraps `Unity.Notifications.NotificationCenter` (Unified API). All `using` + API calls behind `#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR`. Permission cached in save under `notification.permission.requested` — bootstrap step does NOT auto-request; consumer triggers at a "value moment".
- Mock impls remain available behind `ZERO_USE_MOCK_INPUT` / `ZERO_USE_MOCK_AUDIO` / `ZERO_USE_MOCK_NOTIFICATION` defines.
- Docs: input, audio, notification, manual checklist for device-only verification (tap/swipe/pinch, audio crossfade, notification delivery on iOS + Android).

### Phase 3 — UI Scaffolding

- `IUIService` (impl `UIService` in `Zero.UI`) manages popup stack (`PushAsync<TPopup, TData, TResult>`), fullscreen screens, toast queue.
- Layer canvases are NOT spawned by the framework (round-4 refactor). Consumer attaches a `UIRoot` MonoBehaviour to their scene with four Transform inspector slots (Hud / Popup / Overlay / System); `UIRoot.OnEnable` calls `IUIService.AttachRoot`. There is no `UIStep` in the bootstrap pipeline.
- Popup / screen / toast prefabs are loaded from Addressables key conventions (`ui/popup/<name>` etc.) with `HasKeyAsync` pre-check.
- Components ship without prefabs: `LoadingScreenView`, `SafeAreaFitter`, `LocalizedText`. Consumer wires them in their own scene.
- Transitions via LitMotion (`UITransitions.FadeIn/Out` etc.).
- Docs: popup-stack, ui-root, loading-screen, safe-area, toast, localized-text.

### Phase 4 — Gameplay Scaffolding

- `IGameStateMachine` (impl `GameStateMachine`): flat states with sequential `ExitAsync` → `EnterAsync` and an `OnStateChanged` (R3) observable. **Concurrent `ChangeStateAsync` is rejected** with `InvalidOperationException` (no implicit queue — consumer awaits the previous call). Same-instance re-entry rejected.
- `IGameState` (`EnterAsync` / `ExitAsync` / `Tick(deltaTime)`) — consumer drives `Tick` from their own update loop; the state machine is not a MonoBehaviour.
- `ILevelDefinition` abstract `ScriptableObject` (`Id`, `DisplayName`, `AddressablePrefabKey`); `LevelLoader` wraps `IAssetService` and returns `(GameObject Instance, IAssetHandle<GameObject> Handle)` — caller owns disposal.
- 5 lifecycle event POCOs on the bus: `LevelStarted`, `LevelCompleted`, `LevelFailed`, `LevelRestarted`, `LevelExited`.
- 5 sample state shells (`BootState` / `MenuState` / `PlayState` / `PauseState` / `ResultState`) — reference-only, consumer replaces.
- Docs: state-machine, level-loading.

### Phase 5a — Live-Ops + DevTools

- `IVersionCheckService` (impl `VersionCheckService`) compares `Application.version` vs remote-config keys `min_version`, `recommended_version`, `maintenance_mode`. Status precedence: `Maintenance` > `ForceUpdate` > `SoftUpdate` > `Ok`. Malformed/missing keys → warn + `Ok`. Bound via `RegisterFactory` because the ctor takes `string localVersion`. `VersionCheckStep` is non-critical and runs after `RemoteConfigStep`. **The step does not show UI** — consumer reads `LastResult` and decides routing.
- `Zero.DevTools` ships `CheatConsole` (tilde / 4-finger touch toggle), `FpsOverlay` (F2 toggle), command registry, and four built-in commands: `loc set <locale>`, `version check`, `fps show/hide`, `save reset` (stub — extend per-game). Asmdef gated by `defineConstraints: ["UNITY_EDITOR || DEVELOPMENT_BUILD"]` so it strips from production builds. Spawned via `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`. Commands discovered via reflection scan and instantiated via `Container.Construct(Type)` (not `Resolve` — commands aren't registered as contracts).
- Docs: services/version-check, services/time, liveops/version-check, liveops/addressables-remote, dev/cheat-console, dev/fps-overlay.

### Phase 5b — Cross-Cutting Docs

- `README.md` rewritten with stack table, architecture diagram, expanded Quick Start (incl. `ZeroSecrets.asset` setup), phase status, full doc index.
- `README.vi.md` (Vietnamese pitch + Quick Start).
- `LICENSE` (MIT).
- `CONTRIBUTING.md` (service-add recipe, mock SDK extension flow, test conventions, phase workflow, commit-message style).
- `CHANGELOG.md` (this file).
- `docs/architecture/asmdef-graph.md` — final DAG, all asmdefs and edges.
- 8 Mock SDK extension recipes (Crashlytics, Consent, RemoteConfig, Analytics, Attribution, Ads, IAP, ReceiptValidator) under `docs/services/`.
- `docs/meta/recipes.md` — pseudo-code patterns for wallet / progression / variants / daily login (no impl ships, per `PLAN.md` §2.4).

### Notes

- **Meta layer is intentionally out of scope** — `Zero.Meta.asmdef` is an empty placeholder. Hybrid casual and puzzle have meta loops different enough that any "generic" meta would be sludge most consumers rewrite. Recipes in `docs/meta/recipes.md` show how to wire meta on top of `ISaveService` + `IEventBus` + `IRemoteConfigService` per game.
- **Real SDK adapters are not shipped.** Mock impls plus per-mock recipes are the extension surface. Two real-impl exceptions are notification (Unity Mobile Notifications) and localization (Unity Localization) — both wrap official Unity packages.
- The plan in `docs/dev/PLAN.md` is the authoritative architectural decision log. Every phase entry in `docs/dev/JOURNAL.md` records what shipped, what bugs the review caught, and the resume hint for the next session.
