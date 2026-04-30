# Changelog

All notable template-level changes are recorded here. Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) loosely; per-phase deltas with file-level detail and bug retros live in [`docs/dev/JOURNAL.md`](docs/dev/JOURNAL.md).

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
