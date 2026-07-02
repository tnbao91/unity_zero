# Changelog — `com.tnbao91.nobody.zero`

Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) loosely; per-phase implementation deltas live in [`docs/dev/JOURNAL.md`](https://github.com/tnbao91/unity_zero/blob/main/docs/dev/JOURNAL.md) at the repo.

## [Unreleased]

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
