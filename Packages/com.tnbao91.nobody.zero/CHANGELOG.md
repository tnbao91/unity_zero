# Changelog — `com.tnbao91.nobody.zero`

Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) loosely; per-phase implementation deltas live in [`docs/dev/JOURNAL.md`](https://github.com/tnbao91/unity_zero/blob/main/docs/dev/JOURNAL.md) at the repo.

## [Unreleased]

## [0.7.0] — 2026-07-26 — Boot in a couple of seconds

### Changed
- **The pipeline runs in two phases.** Only `Log`, `DeviceProfile`, `Save` and `Asset` are *blocking*; the other twelve steps are *deferred* and run after `BootstrapReady`, while the player is already in the game. Before this, all 16 steps sat on the critical path with a 30s timeout and 2 attempts each — a **940-second worst case** for a genre that should boot in about a second. An audit of what each step costs and what depends on it found only **three real ordering constraints** (`Save→Audio`, `Save→Notification`, `RemoteConfig→VersionCheck`) and **nine steps that nothing resolves at all**.
- **Per-step `Timeout` default 30s → 10s.** A step that needs longer is telling you it belongs in the deferred phase.
- **`ConsentStep.Timeout` → `TimeSpan.Zero` (no deadline).** A real UMP/ATT flow puts a modal dialog in front of a human; a step timeout would cancel it mid-read. Safe only because the step is now deferred and blocks nobody.
- The deferred phase runs **sequentially**, not concurrently — the tail costs the player nothing, and declared order preserves `RemoteConfig→VersionCheck` for free without a dependency API.

### Added
- **`BootstrapPhase` on `IBootstrapStep`** (`Blocking` | `Deferred`). `BootstrapStepBase.Phase` defaults to `Blocking` so an existing consumer step keeps its current semantics rather than silently sliding off the boot path.
- **`BootstrapReady(BlockingMs)`** on `IEventBus` — fires when the blocking phase ends, carrying how long the player actually waited. This is the signal to leave the splash screen; before 0.7.0 boot completion produced one log line and nothing else.
- **A blocking-phase budget** (`BootstrapPipeline.DefaultBlockingBudget`, 5s). On expiry, unfinished and unstarted blocking steps are recorded degraded and `BootstrapReady` fires anyway. This is what actually bounds time-to-first-screen, however many blocking steps a consumer adds.
- `BootstrapPipeline.DeferredCompletion` — observes the background phase. Nothing needs to await it to play.
- `BootstrapPhaseTests` + `BootstrapBugRegressionTests` (~12 EditMode tests), including a classification pin so the blocking set cannot silently regrow.

### Fixed
- **`AudioMixerService.InitializeAsync` leaked a GameObject pair on every call.** No idempotency guard: each invocation created a fresh `[Zero.AudioMusic]` and `[Zero.AudioSfxSource]` with `DontDestroyOnLoad` and overwrote `_mixerHandle` without disposing it. The `BootstrapRetryRequested` path re-runs every step, so this was reachable in shipped builds.
- **`EncryptedJsonSaveService` threw from its constructor.** In a player build with a missing or placeholder `ZeroSecrets.asset`, `LoadSeeds` threw during Reflex container resolve — before step 1, outside the pipeline — surfacing as an opaque resolve failure with no `BootstrapFailed`, no degradation and no pipeline log. Seed derivation now happens on first use, inside `SaveStep`, where it is reported like any other step failure. Same exception, same message, same fatality.

### Migration
**If your step derives from `BootstrapStepBase`** (the documented way) it keeps compiling and behaving exactly as before — `Phase` defaults to `Blocking`, which is the old semantics.

**If your step implements `IBootstrapStep` directly, it will not compile.** Adding `Phase` to the interface is a source-breaking change for direct implementers; you get `CS0535: does not implement interface member 'IBootstrapStep.Phase'`. Add one line:

```csharp
public BootstrapPhase Phase => BootstrapPhase.Deferred;   // or Blocking to keep old behaviour
```

Then **review every step you own and set `Phase => BootstrapPhase.Deferred` on everything the first screen does not need** — that is where the boot-time win actually is. Adding a step to the blocking phase is a decision to make the player wait for it.

To hand off to your game, subscribe `BootstrapReady` instead of guessing when the pipeline is done:

```csharp
_bus.On<BootstrapReady>().Subscribe(e => {
    Debug.Log($"Player waited {e.BlockingMs:F0}ms");
    LoadHomeScene();
});
```

## [0.6.0] — 2026-07-26 — Bootstrap never blocks the player

### Changed
- **No shipped bootstrap step is critical any more.** `DeviceProfileStep`, `AssetStep` and `ConsentStep` were critical; all three are now non-critical. A critical failure aborts the pipeline, and since nothing in the template subscribes to `BootstrapFailed` and `LoadingScreenView` has no failure path, an abort left the player on the splash screen indefinitely. For a hybrid-casual/puzzle template that is the worst available outcome, and it could be triggered by something as minor as quality settings failing to apply.
- `AssetStep.MaxRetries` 1 → **2** (3 attempts). A transient Addressables catalog fetch is the failure most worth retrying.

### Added
- **`IBootstrapReport`** (`Zero.Core`) — durable record of which steps degraded, so "did anything fail?" is answerable after boot. `IsHealthy`, `Degraded`, `IsDegraded(stepName)`. Registered as a singleton; impl `BootstrapReport` in `Zero.Infrastructure`. Needed because `R3EventBus` does not replay: anything constructed after boot has already missed the event.
- **`BootstrapStepDegraded(StepName, Error, Attempts)`** — published when a step exhausts its retries, so continuing past a failure is an observable decision rather than a silent one. Previously this path only wrote a `Warn` line.
- `BootstrapDegradationTests` — 10 EditMode tests pinning both halves of the contract: the pipeline continues, and the failure is visible. Includes a test asserting every shipped step is non-critical, so the rule cannot regress by accident.

### Migration
Nothing to change unless you relied on bootstrap aborting. If you did, two options: keep the behaviour by registering your own critical step via `BootstrapStepRegistration`, or — better — read `IBootstrapReport.IsDegraded("<step>")` in your first scene and degrade the affected feature instead of the whole launch.

**`ConsentStep` going non-critical shifts an obligation onto you.** The game now runs when the consent form could not load. The legal duty is "do not track without consent", not "do not run without consent", so whatever you bind for ads / analytics / attribution must default to **non-personalized** when consent is unresolved. Gate personalization on `IBootstrapReport.IsDegraded("Consent")`.

## [0.5.2] — 2026-07-26 — GUID collision with Addressables 3.0.0

### Fixed
- **`Zero.UI` failed to compile in any project also using `com.unity.addressables` 3.0.0.** `Runtime/UI/PopupHandle.cs.meta` shipped a hand-authored GUID (`c2d3e4f5a6b7c8d9e0f1a2b3c4d5e6f7`) that is byte-identical to the one in Addressables 3.0.0's `Tests/Editor/ProjectConfigDataSerializationTests.cs.meta`. Unity resolved the conflict by dropping `PopupHandle.cs` from the AssetDatabase, so `PopupBase.cs` and `UIService.cs` failed with `CS0246: PopupHandle<> / IPopupHandle could not be found` — a hard compile error blocking the whole Editor. GUID regenerated. (Addressables 2.3.1 and earlier do not contain that test file, which is why this only surfaced on the 3.0.0 upgrade.)
- `Runtime/UI/LocalizedText.cs.meta` carried a second hand-authored GUID (`f1a2b3c4…`) with the same collision risk; also regenerated. No asset in the package or in consumer projects referenced either GUID, so no prefab/scene rewiring was needed.

## [0.5.1] — 2026-07-02 — AdPlacement boundary guards

### Fixed
- **`DefaultAdPlacementService` no longer throws on a null placement id.** `CanShow(null)` / `TryShowAsync(null)` / `NotifyShown(null)` crashed with `ArgumentNullException` out of the internal dictionary, contradicting the documented fail-safe contract (`docs/services/adplacement.md`). Queries now fail safe (`false` / `Failed` result / no-op); `RegisterPlacement` validates input (`ArgumentNullException`/`ArgumentException` on null/empty id, `ArgumentOutOfRangeException` on `sessionCap < 1` or negative cooldown).

### Added
- `AdPlacementServiceTests` (EditMode, 15 methods) — boundary guards + session-cap/cooldown/re-register/unknown-placement semantics. The service previously had no tests.

### Changed
- Inert `"R3"` entries removed from `Zero.Editor` and both test asmdefs (completes the v0.3.0 round-C sweep; R3 is a NuGetForUnity DLL, not an asmdef).
- `ConsoleCommandAttribute` is now `sealed`.

## [0.5.0] — 2026-06-20 — Unity 6.5 upgrade

Minimum Unity version raised to 6000.5.0f1. No change to public API.

### Changed
- **`UnityPoolService`: `GetInstanceID()` → `GetEntityId()`.** Unity 6.5 deprecates `Object.GetInstanceID()` (CS0619) in favour of the 64-bit `EntityId` struct. Internal pool dictionaries split into typed `_goPools`/`_wrapPools` — the int-XOR key for component wrappers is gone. Public `IPoolService` unchanged.
- **Minimum Unity version: 6000.3 → 6000.5.** Consumers on Unity 6.3 must stay on 0.4.x.
- Package dep bumps auto-resolved by Package Manager on upgrade: LitMotion 2.0.1→2.0.2, R3 1.3.0→1.3.1, UniTask 2.5.10→2.5.11, Reflex 14.3.0→14.3.1, Localization 1.5.11→1.5.12, Purchasing 5.2.1→5.3.1, URP 17.3.0→17.5.0, Test Framework 1.6.0→1.7.0, uGUI 2.0.0→2.5.0.

## [0.4.0] — 2026-06-11 — Production hardening

Mostly drop-in; read the three action items. Additive public API (minor bump): `BootstrapFailed` / `BootstrapRetryRequested` events, `BootstrapStepFailedException`, `BootstrapStepRegistration` + `BootstrapStepComposer`.

**Action required for production games:**
1. **Mobile save flush:** call `_save.SaveAsync().Forget()` from `OnApplicationPause(true)` — suspended apps are killed without callbacks and there is **no automatic pause save** (recipe in `docs/services/save.md`). `Dispose()` now flushes a pending debounced save, but that only covers desktop/editor quits.
2. **IL2CPP `link.xml`:** types you persist via `ISaveService` are deserialized through reflection and get stripped on device builds. Import the updated `BootstrapScene` sample for the `link.xml` template.
3. **If you swapped a real crash SDK in:** `CrashlyticsStep` is now non-critical with a 5s timeout (was critical/30s) so a vendor outage can't block launch. Need the old behavior? Re-register the step via `BootstrapStepRegistration(..., BootstrapStepAnchor.Replace, "Crashlytics")` with your own criticality.

### Added
- **Bootstrap failure/retry seam:** critical aborts publish `BootstrapFailed {StepName, Error, Attempt}` on `IEventBus` and throw `BootstrapStepFailedException`; publish `BootstrapRetryRequested` to make `GameLauncher` re-run the pipeline. Wire a retry button into your loading screen — steps re-run from the top, keep them idempotent.
- **Bootstrap-step seam:** add/insert/replace pipeline steps from YOUR asmdef by registering `BootstrapStepRegistration` in your `OnRootContainerBuilding` installer — no fork, no partial. Recipe: `ClaudeMemory` sample → `extension-points.md` §5.
- `Samples~/BootstrapScene/link.xml` template for save-model stripping.

### Changed
- `ProjectScopeInstaller.Hook` now registers at `BeforeSplashScreen`, making your installer's re-registrations (last-write-wins) deterministic — previously cross-assembly ordering was unspecified.
- `CrashlyticsStep` non-critical + 5s timeout (see action item 3).
- **`ClaudeMemory` sample corrected — re-import it.** The "extend `ProjectScopeInstaller` via `UserServices.cs` partial" advice was invalid C# for package consumers (partials can't span assemblies); recipes now use `OnRootContainerBuilding` + `BootstrapStepRegistration`. Sample code also fixed against the real interfaces (`ISaveService.TryGet` instead of a nonexistent `Get<T>(key, default)`, real `ICrashlyticsService` members, no pause auto-save claim, no subclassing the sealed save service).

### Fixed
- Corrupt/tampered saves are quarantined to `save.dat.corrupt` before reset-to-empty — your support flow finally has a recovery artifact.
- `Dispose()` flushes a `RequestSave` still inside the 1s debounce window (desktop/editor quit path).
- Cancelling a popup push while another popup was open could evict the wrong popup's bookkeeping and close the wrong popup on the next `PopAsync` — fixed by remove-by-reference in `UIService` and `PopupStack`.
- `LogService.Error(null)` no longer forwards null into `Debug.LogException`; empty-text toasts ignored with a warning; toast durations clamp to ≥0.5s.

## [0.3.0] — 2026-05-31 — AI agent harness guardrails

Safe drop-in upgrade — no runtime/service behavior change. The consumer-facing delta is in the `ClaudeMemory` sample.

### Added
- `Samples~/ClaudeMemory`: three game-tuned slash commands — `/phase-open` (branch + name RED-first behavior tests), `/phase-close` (game-lead pre-merge audit), `/pre-pr` (fan out boundary + pitfalls reviewers, then `/code-review`) — and a `.claude/settings.example.json` `deny` rule blocking edits inside the package. **Re-import the sample** (Package Manager → Zero → Samples → ClaudeMemory) to pick these up.

### Changed
- Internal refactor, no behavior change, safe drop-in: de-duplicated the EditMode-safe destroy guard into `Zero.Infrastructure.Util.SafeDestroy(GameObject)`. Replaces the byte-identical `Zero.UI.UiObjects.SafeDestroy` (`internal`, now removed) and `UnityPoolService.SafeDestroy` (`private`). No behavior change. Net consumer API delta: one new `public` helper `Zero.Infrastructure.Util.SafeDestroy(GameObject)` (nothing removed from the consumer surface).

## [0.2.3] — 2026-05-17

Post-review cleanup — no behavior change to shipped services. Safe drop-in upgrade.

### Removed
- Example Gameplay state shells (`BootState` / `MenuState` / `PlayState` / `PauseState` / `ResultState`). Gameplay states are consumer-authored; `GameStateMachine` + `LevelLoader` scaffold and lifecycle events are unaffected. Consumer-visible API removal → patch bump.

### Fixed
- `Zero.UI` (`UIService`, `ScreenManager`, `ToastQueue`) + `AudioMixerService`: EditMode-safe destroy via new `Zero.UI.UiObjects.SafeDestroy`; PlayerLoop-await guards (`Application.isPlaying`).
- `AudioMixerService` music/SFX paths: `HasKeyAsync` pre-check before `LoadAsync<AudioClip>`.

### Changed
- Service docs corrected to the binding-swap / decorator model on `sealed` impls (no "subclass / override").
- Removed inert `"R3"` entry from `references[]` in 18 runtime asmdefs (R3 is a NuGetForUnity DLL, auto-included).

### Notes
- Full detail in [`docs/dev/JOURNAL.md`](https://github.com/tnbao91/unity_zero/blob/main/docs/dev/JOURNAL.md).

## [0.2.2] — 2026-05-12

Documentation restructure — no runtime / API changes. Safe drop-in upgrade.

### Changed
- `Samples~/ClaudeMemory/CLAUDE.md` slimmed from 113 → 55 lines. Now a constitution (principles + anti-patterns + references); cheatsheets that duplicated `claude-context/*` removed. Consumers re-importing the sample get the leaner version.

### Notes
- No `Runtime/` or `Tests/` changes. Existing consumer code keeps working unchanged.

## [0.2.1] — 2026-05-10

Hotfix: restore `Runtime/Services/Log/` folder (6 files) lost during the v0.1.0 UPM restructure due to a global gitignore `log` pattern collision on case-insensitive macOS. v0.1.0 and v0.2.0 ship with the broken state and should NOT be installed; v0.2.1 is the first installable release. `.gitignore` now negates the path to prevent recurrence.

## [0.2.0] — 2026-05-10

Adds `Samples~/ClaudeMemory/` AI agent context bundle for Claude Code-assisted consumer development.

### Added
- `Samples~/ClaudeMemory/` — `CLAUDE.md` + `claude-context/` (architecture, available-services, extension-points, stack-constraints, pitfalls) + `.claude/settings.example.json`.
- `package.json` registers the new sample.
- README section advertises AI agent compatibility.

### Notes
- Consumer imports the sample, moves `CLAUDE.md` + `claude-context/` to their repo root. Claude Code auto-reads them at session start.
- Claude-only scope. Cross-tool agent docs (`AGENTS.md`, Cursor rules, Copilot instructions) deferred.

## [0.1.0] — 2026-05-10

Initial release as Unity Package Manager package. Restructured from full-project template to embedded-package layout for `git+https://...?path=Packages/...` install + OpenUPM publish. All v0 template features carried over from the prior project-based release.

### Added
- `package.json` declaring 11 OpenUPM/Unity dependencies (UniTask, R3, ZString, Reflex, LitMotion, Addressables, Localization, Mobile Notifications, Newtonsoft, Purchasing, Input System).
- `Samples~/BootstrapScene/` bundle: Bootstrap.unity, ZeroSecrets.asset.example, ReflexSettings.asset, packages.config (consumer-side NuGet restore).
- 5-step consumer Quick Start in package README.

### Notes
- BCL transitive deps for R3 (`Microsoft.Bcl.AsyncInterfaces`, `Microsoft.Bcl.TimeProvider`, `System.ComponentModel.Annotations`, `System.Threading.Channels`) require NuGetForUnity prereq; not bundled to avoid duplicate-DLL conflicts when consumer follows R3's OpenUPM README.
- Documentation lives at [GitHub `docs/`](https://github.com/tnbao91/unity_zero/tree/main/docs); package tarball does not include offline docs in v0.1.0.
