# Implementation Journal — Unity Zero v2

Append-only log of phase implementations. One entry per phase complete.

**Format:**
```
## Phase <N> — YYYY-MM-DD (commit <sha-short>)
- Branch: <branch-name>
- Files touched: <list>
- Key decisions: <deviations from plan if any>
- Tests: <X/Y passing>
- Resume hint: <next phase prerequisite, known gotchas>
```

**Source of truth for plan:** [`PLAN.md`](./PLAN.md). Read this journal's tail + the plan to resume work after a session clear.

---

## Phase 1a — 2026-04-28 (commit c0ee281)
- Branch: `phase-1a-foundation`
- Files touched:
  - New interfaces in `Zero.Core`: `IEventBus`, `IL10nService`, `IBootstrapProgressReporter`; expanded `IInputService`; added `Timeout`/`MaxRetries` to `IBootstrapStep`.
  - New asmdef `Zero.Services.Events` with `R3EventBus` (typed `Dictionary<Type, object>` storing `Subject<T>`).
  - New asmdef `Zero.Services.Localization`: `UnityLocalizationService` (wraps `com.unity.localization`), `MockLocalizationService`, `LocalizationStep` (awaits `LocalizationSettings.InitializationOperation`).
  - `Zero.Infrastructure.BootstrapProgressReporter` (Singleton; pipeline writes, views read).
  - `BootstrapPipeline`: per-step `Progress<float>` slicing, `CancelAfter(step.Timeout)`, retry loop honoring `MaxRetries` for non-critical steps.
  - `ProjectScopeInstaller`: registered Events + Localization installers; reordered step list (Save now right after DeviceProfile per PLAN 2.11).
  - Asmdef peer restructure: `Zero.Gameplay`/`Zero.Meta`/`Zero.UI` no longer cross-reference each other; all reference `Zero.Services.Events` instead.
  - Pool refactor: `ReflexPoolService` → `UnityPoolService`, internal storage `Stack<GameObject>` → `UnityEngine.Pool.ObjectPool<GameObject>` with compile-time-gated `collectionCheck`. `IPoolService` interface unchanged.
- Key decisions:
  - Pool `actionOnGet` left null; `SetActive(true)` happens in `Spawn()` *after* `SetParent` + `SetPositionAndRotation` so `OnEnable` sees the requested transform (fixed in c0ee281 after initial refactor activated too early).
  - `createFunc` instantiates parented to `[Zero.Pools]` root and immediately `SetActive(false)` to prevent the just-created object flashing active before first Spawn.
  - Pool defaults: `defaultCapacity: 10`, `maxSize: 10000` — to be documented in `docs/services/pool.md` during Phase 1b.
- Tests: none yet (Phase 1b ships the 4 EditMode test suites).
- Late-phase fixes (after first Press Play surfaced compile + runtime errors):
  - `Zero.Services.Localization.asmdef` — added `Unity.ResourceManager` (for `AsyncOperationHandle<>`) and `UniTask.Addressables` (for `.ToUniTask()` extension on it).
  - `UnityLocalizationService` — collapsed to single `(ILogService)` ctor; the `(ILogService, string)` overload made Reflex try to resolve `String` from the container and fail.
  - `LocalizationStep` — guard with `LocalizationSettings.HasSettings` + try/catch so a fresh template (no Localization assets / Addressables not built) logs a warning instead of red `InvalidKeyException`.
- Verification: User confirmed Editor compile clean + Bootstrap.unity Press Play runs through all steps with no console errors. Asmdef DAG verified: no `Zero.UI`/`Zero.Meta` refs in `Zero.Gameplay.asmdef`; no `Zero.Meta` ref in `Zero.UI.asmdef`. `grep -rn ReflexPoolService Assets/` returns zero matches.
- CLAUDE.md refreshed alongside this entry (commit `e771e95`) — peer asmdef diagram, bootstrap timeout/retry, real-impl exceptions list.
- Resume hint: Phase 1b is next — save seed hardening (`Resources/ZeroSecrets`), 4 EditMode test suites (Save/Pool/BootstrapPipeline/EventBus), CI workflow, 8 doc files. Phase 1b should land on a new branch `phase-1b-tests-docs`.

---

## Phase 1b — 2026-04-28 (commits babace5, 2557802, 234b54a, 8688cd8)
- Branch: `phase-1b-tests-docs`
- Files touched:
  - New: `Assets/_Project/Scripts/Runtime/Services/Save/ZeroSecrets.cs` (ScriptableObject for per-game seeds)
  - New: `Assets/Resources/ZeroSecrets.asset.example` (template with placeholder marker)
  - New: 4 EditMode test files (SaveServiceTests, PoolServiceTests, BootstrapPipelineTests, EventBusTests)
  - New: `.github/workflows/tests.yml` (game-ci/unity-test-runner, EditMode only)
  - New: `README.md` (minimal, mentions CI + Quick Start)
  - New: 8 module documentation files under `docs/`:
    - `docs/architecture/event-bus.md`, `docs/architecture/bootstrap-pipeline.md`
    - `docs/services/save.md`, `docs/services/localization.md`, `docs/services/pool.md`
    - `docs/security/save-encryption.md`
    - `docs/testing/writing-tests.md`, `docs/testing/ci.md`
  - Edit: `EncryptedJsonSaveService` — read seeds from `Resources/ZeroSecrets`; throw in player builds if missing/placeholder; warn + fallback in Editor.
  - Edit: `Zero.Tests.EditMode.asmdef` — added service references needed by tests.
  - Edit: `CLAUDE.md` — refreshed save seed section, added CI notes, added docs notes.
- Key decisions:
  - Tests use actual `Application.persistentDataPath` but clean up in `[TearDown]` to avoid pollution.
  - Event tests verify typed `Subject<T>` in bus works; record struct events fully supported (no boxing caveat in docs).
  - Bootstrap tests cover full pipeline: order, abort, swallow, progress, cancel, timeout, retry.
  - Documentation format locked: every module doc has Overview (2-3 sent), Public API, Extension Points, Examples, Known Limitations, Design Rationale.
  - README is minimal (no full setup guide — that's Phase 5 cross-cutting); mentions CI license requirement.
- Tests: 4 test suites, ~22 test cases total. Cover SaveService round-trip + tamper + reload smoke; PoolService LIFO + prewarm + active/inactive flags + dispose idempotency; BootstrapPipeline order + critical-abort + non-critical-swallow + monotonic progress + outer-cancel + critical-timeout + retry-then-success; EventBus pub/sub + multi-sub + type isolation + dispose + value-type + late-subscriber.
- Opus review (commit `22eb399`): the four test files Haiku originally produced did **not** compile against the real public APIs (wrong `OnExecuteAsync` signature, wrong `RunAsync` arity, `IPool<T>.Get/Release` instead of `Spawn/Despawn`, `IPoolService.GetPool<T>()` missing prefab arg, `StubLogService` missing `IsEnabled` + had a fictional `Debug` method). Two assertions were also logically wrong (`TimeoutFires` expected non-critical timeout to abort the pipeline; `RetryPolicy` had off-by-one on the flaky-step counter). All four suites rewritten end-to-end against the actual contracts. Pool doc and bootstrap-pipeline doc signatures corrected. `ZeroSecrets.cs.meta` pre-committed with deterministic GUID so `ZeroSecrets.asset.example` resolves the script reference on first project open.
- Migration test gap: `EncryptedJsonSaveService.Migrate(...)` is `private`, so the Phase 1b plan's "write v0 file → assert Migrate ran" coverage isn't reachable from the test asmdef. Replaced with a load→save→reload smoke test plus a "Known Limitations" note in `docs/services/save.md`. Promoting `Migrate` to `protected virtual` is a Phase 2+ candidate if migration coverage matters.
- Verification: 22/22 EditMode tests green on user's machine after the fixes below landed. Test Runner discovered the suite once `includePlatforms: ["Editor"]` was restored AND the NuGet plugin metas for R3 + its transitive deps were patched to `Editor.enabled: 1` (NuGetForUnity defaults Editor=0, which is too defensive for assemblies the project actually consumes from editor scripts).
- Production bugs the new tests caught (would have shipped silently otherwise):
  1. `UnityPoolService.EnsureRoot` called `DontDestroyOnLoad` unconditionally — throws `InvalidOperationException` from any editor script. Now gated behind `Application.isPlaying`.
  2. `UnityPoolService.Dispose` and the `ObjectPool.actionOnDestroy` callback both called `Object.Destroy`, play-mode-only. Routed through a `SafeDestroy` helper that picks `DestroyImmediate` in EditMode.
  3. `PrewarmAsync` used `UniTask.Yield(ct)` with the default `PlayerLoopTiming.Update`. EditMode does not tick that timing, so the await silently aborted the prewarm loop after the first iteration. Now skipped in EditMode; in play mode the breathe fires between chunks.
  4. `GameObjectPool.Prewarm` looped `Get → Release` `count` times. Because every `Release` pushes onto `UnityEngine.Pool.ObjectPool`'s internal stack, the next `Get` pops the same instance — `CountInactive` ended at 1 regardless of `count`. Real prewarm has to hold N instances out simultaneously and release them in one go to force `createFunc` to run N times. Refactored.
- Other late fixes (compile/discovery, not production bugs): added `using R3;` to `BootstrapPipelineTests` so `Subscribe(Action<T>)` extension resolves; flipped `overrideReferences` to `false` on the test asmdef and added `R3.dll` linkage via the NuGet meta patch.
- Resume hint: Phase 2 — real Input + Audio + Notification services + manual checklist + integration tests on branch `phase-2-real-services`.

---

## Phase 2 — 2026-04-29 (branch `phase-2-real-services`, commits 0bbc9ce…3a82025, **awaiting Editor verification before merge**)

- Branch: `phase-2-real-services` (10 commits; not yet merged to `main`)
- Files touched:
  - New: `Assets/_Project/Scripts/Runtime/Services/Input/UnityInputService.cs` — wraps Unity Input System + EnhancedTouch. Internal MonoBehaviour `InputDriver` polls per-frame; `DontDestroyOnLoad` guarded behind `Application.isPlaying`. Gesture thresholds: tap = down→up <200ms AND drag <20px; swipe = drag ≥50px in <500ms; drag = motion-while-pressed; pinch = two-finger active-touch distance ratio per frame.
  - New: `Assets/_Project/Scripts/Runtime/Services/Audio/AudioMixerService.cs` — loads `audio/main_mixer` via `IAssetService` (defensive warn-and-fallback if missing), bus volumes persisted via `ISaveService` keys `audio.bus.{bus}`, music source with LitMotion `BindToVolume` fade in/out, SFX via `IPoolService.GetPool<GameObject>` template (one persistent template GO, `GetComponent<AudioSource>()` after Spawn). All `IAssetHandle<T>` are tracked + disposed.
  - New: `Assets/_Project/Scripts/Runtime/Services/Notification/UnityMobileNotificationService.cs` — uses **Unified API** (`Unity.Notifications.NotificationCenter`) with `Initialize`, `RequestPermission` (returns `NotificationsPermissionRequest` whose `Status == NotificationsPermissionStatus.Granted` indicates grant), `ScheduleNotification`/`CancelScheduledNotification`. Permission outcome cached in `ISaveService` key `notification.permission.requested`. String → int id mapping via `_scheduledIds` Dictionary. **All `using Unity.Notifications;` and API call bodies wrapped in `#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR`** because the Unified asmdef has `includePlatforms: ["Android","Editor","iOS"]`; without the guard, Standalone/WebGL builds break.
  - Edit: 3 service installers wrapped real-impl bindings in `#if !ZERO_USE_MOCK_<X>` (`INPUT`/`AUDIO`/`NOTIFICATION`) so headless / Mock fallback stays available.
  - Edit: `Bootstrap/Steps/NotificationStep.cs` — removed auto `RequestPermissionAsync`; only `InitializeAsync` runs at bootstrap. Comment points to `docs/services/notification.md` for value-moment permission flow.
  - Edit: 3 asmdef references — `Zero.Services.Input` adds `Unity.InputSystem`; `Zero.Services.Audio` adds `LitMotion`, `LitMotion.Extensions`; `Zero.Services.Notification` adds `Unity.Notifications.Unified`. `Zero.Tests.EditMode.asmdef` updated to reference the three service asmdefs.
  - New: 3 EditMode test files — `InputGestureTests.cs` (pure tap/swipe classification logic), `AudioBusPersistenceTests.cs` (bus persistence round-trip via in-memory `ISaveService` stub + clone of `MockAudioServiceForTesting` since real mixer can't load headless), `NotificationPersistenceTests.cs` (permission caching, schedule/cancel stubs). All stubs are `private` nested classes inside the test class to avoid namespace-level duplicate-type collisions across files.
  - New (docs): `docs/services/{input,audio,notification}.md` (fixed format) + `docs/testing/manual-checklist.md` (per-feature device verification steps for tap/swipe/pinch, audio bus persistence, music crossfade, iOS/Android notification delivery).
- Workflow: spawned a **Haiku junior-dev** subagent to implement, then **Opus lead** review — 17 bugs found in round 1 (P0 compile breakers + logic) and Haiku patched them in 4 commits; Opus second review found 4 more issues (asmdef ref, Status vs Granted, platform ifdef, doc drift) which Haiku patched in 3 more commits. Each round was committed incrementally so review stays auditable.
- Production bugs the review caught (would have shipped silently otherwise):
  1. `IAssetService.LoadAsync<T>` returns `IAssetHandle<T>`, not `T` — every call site was wrong (mixer, music clip, sfx clip).
  2. `IPoolService.GetPool(GameObject)` returns `IPool<GameObject>`, not `IPool<AudioSource>` — type mismatch wouldn't compile.
  3. LitMotion namespace is `LitMotion`/`LitMotion.Extensions`, not `AnnulusGames.LitMotion`. The audio extension is `BindToVolume(AudioSource)`, not `BindToAudioSourceVolume`.
  4. `Unity.Notifications` package classes invented out of training-data familiarity — `GameNotification`/`GameNotificationRequest` don't exist; correct path is the Unified API or per-platform `iOSNotification`/`AndroidNotification`.
  5. `NotificationsPermissionRequest.Granted` doesn't exist — actual API is `Status == NotificationsPermissionStatus.Granted`.
  6. `Unity.Notifications.Unified.asmdef` has `includePlatforms: ["Android","Editor","iOS"]` — referencing it without ifdef guards breaks Standalone/WebGL builds.
  7. SFX `PlaySfxAsync` had no try/finally — a cancelled `UniTask.Delay` would leak the pooled source and asset handle.
  8. `Dispose` originally leaked the SFX template GO and both asset handles.
  9. EditMode test files declared duplicate `internal sealed class StubSaveService`/`StubLogService` at namespace level → C# compile error. Fixed by moving stubs inside the test class as `private nested`.
  10. Test stub `IAssetService` impl had a fictional `Release<T>` method and `LoadAsync<T>` returning `T` — must return `IAssetHandle<T>` and add `ActiveHandleCount`/`InitializeAsync`/`PreloadAsync`.
- Verification:
  - **Static**: every API signature in the new code re-read against actual source files in `Assets/_Project/Scripts/Runtime/Core/Interfaces/*.cs` + `Library/PackageCache/com.annulusgames.lit-motion@*` + `Library/PackageCache/com.unity.mobile.notifications@*`. Compile-clean by inspection.
  - **Headless EditMode test run**: blocked — Unity Editor was already running on the user's machine, holding the project lock. The job started but exited before discovery.
  - **Editor verification still owed by user** before merge: open Test Runner, run EditMode suite; open `Bootstrap.unity` Press Play, scan log for `[Bootstrap]` ordering + no exceptions.
  - **Manual device verification (PLAN §6 Phase 2 acceptance)** still required after merge: tap/swipe/pinch on iOS/Android, audio bus persistence across restart, music crossfade, notification schedule + delivery on both platforms.
- Resume hint: do not merge `phase-2-real-services` to `main` until user confirms (a) EditMode suite green in Test Runner and (b) Bootstrap.unity Press Play runs through to completion. After merge, Phase 3 — UI scaffolding (popup stack, transitions, loading screen, toast, localized text) on branch `phase-3-ui`.

---

## Phase 3 — 2026-04-30 (pending merge, branch `phase-3-ui`)
- Branch: `phase-3-ui`
- Files touched:
  - Interfaces: `Assets/_Project/Scripts/Runtime/Core/Interfaces/IUIService.cs` (reshaped to generic popup API)
  - Events: `Assets/_Project/Scripts/Runtime/Core/Events/UIEvents.cs` (PopupOpened, PopupClosed, PopupBackdropTapped)
  - Runtime UI (new, under `Assets/_Project/Scripts/Runtime/UI/`):
    - `UIService.cs` — main service, owns PopupStack + ScreenManager + ToastQueue, loads prefabs via Addressables
    - `UiLayer.cs` — layer sort order constants (Hud=100, Popup=200, Overlay=300, System=400)
    - `PopupHandle.cs` — generic result handle for typed popup returns
    - `PopupBase.cs` — abstract MonoBehaviour base + `IPopup<TData, TResult>` interface, virtual transition hooks
    - `PopupStack.cs` — internal FIFO stack with sort order assignment per layer
    - `ScreenManager.cs` — fullscreen one-at-a-time screen loading + optional `IScreenInitializable<TData>`
    - `LayerCanvas.cs` — static helper to runtime-build 4 Canvas GameObjects per layer
    - `LoadingScreenView.cs` — component-only, reads from `IBootstrapProgressReporter` (never resolves pipeline)
    - `SafeAreaFitter.cs` — component-only, fits RectTransform to Screen.safeArea, re-applies on orientation change
    - `LocalizedText.cs` — component-only, subscribes `IL10nService.OnLocaleChanged`, auto-updates TextMeshProUGUI
    - `ToastQueue.cs` — FIFO queue with max-16 cap, loads toast prefab from Addressables key `ui/toast/default`
    - `Transitions/UITransitions.cs` — static LitMotion-backed helpers (FadeIn/Out, SlideIn/Out, ScaleIn/Out)
    - `UIServiceInstaller.cs` — Reflex installer, registers UIService as singleton, lazy
  - Bootstrap: `Assets/_Project/Scripts/Runtime/Bootstrap/Steps/UIStep.cs` — non-critical step, calls `UIService.InitializeAsync`
  - Bootstrap config: updated `ProjectScopeInstaller.cs` (add UIServiceInstaller.Install call, add UIStep to pipeline after LocalizationStep)
  - Tests: `Assets/_Project/Scripts/Tests/EditMode/`:
    - `PopupStackTests.cs` — push/pop ordering, replace, sort order monotonic, queue enqueue/dequeue
    - `ToastQueueTests.cs` — max queue capacity drop behavior (smoke test, async `[UnityTest]`)
    - `UITransitionInterruptionTests.cs` — fade cancellation + sequential transitions after cancellation
  - Asmdef: `Zero.UI.asmdef` (add LitMotion, LitMotion.Extensions, Unity.TextMeshPro, Unity.Addressables); `Zero.Tests.EditMode.asmdef` (add Zero.UI, R3, LitMotion)
  - Docs: new files under `docs/ui/`:
    - `popup-stack.md` — push/pop/replace, backdrop event, custom popup pattern, transition override
    - `loading-screen.md` — component-only contract, no prefab ships, IBootstrapProgressReporter read-only
    - `safe-area.md` — notch support, polling for orientation change, Editor Game view limitation
    - `toast.md` — FIFO queue, Addressables key, auto-dismiss duration, max 16 message cap
    - `localized-text.md` — R3 subscription on locale change, fallback-to-key on missing
  - Updated: `docs/testing/manual-checklist.md` (append Phase 3 UI section), `CLAUDE.md` (add UIService to exceptions + Phase 3 items to "easy to miss")
- Key decisions:
  - `PopupBase<TData, TResult>` is generic two-parameter type (data input, result output) for strong typing. Default transitions (Fade/Slide/Scale) via enum + LitMotion, overrideable via virtual hooks.
  - Modal mask is NOT auto-rendered; consumer includes a raycast-blocker Image in their popup prefab.
  - Layer canvases built at runtime by `LayerCanvas.Build()` in `UIStep` with `DontDestroyOnLoad` (guarded by `Application.isPlaying`). Zero canvases in `Bootstrap.unity`.
  - `LoadingScreenView` injects `IBootstrapProgressReporter` (not `BootstrapPipeline`) to avoid Lazy-singleton resolution races. Component-only; no prefab ships.
  - `SafeAreaFitter` polls Screen dimensions in Update to detect orientation changes (more reliable than `OnScreenOrientationChanged` callback which fires unpredictably).
  - `ToastQueue` uses `HasKeyAsync` pre-check before loading toast prefab to avoid Addressables red errors on missing key. Queues up to 16; older messages drop if exceeded.
  - Popup prefabs loaded from `ui/popup/<name>` (derived from PopupBase subclass name). Screen prefabs from `ui/screen/<name>`. Toast from `ui/toast/default`.
  - `PopupStack.Push` returns assigned sort order (for consumer to apply to Canvas.sortingOrder if needed). Internal; UIService handles sort order assignment.
  - No extension points for PopupStack internals; entire stack logic kept testable without real Canvases (PopupStack works with plain GameObjects).
- Tests: 3 new EditMode suites (PopupStackTests, ToastQueueTests, UITransitionInterruptionTests). Previous 22 from Phases 1a/1b/2 should still pass.
- Deviations from plan:
  - None major. Popup key convention is `typeof(TPopup).Name.ToLowerInvariant()` (not `[PopupKey("name")]` attribute) — simpler, no magic, explicit per-type.
  - `ScreenManager.IScreenInitializable<TData>` optional interface (not mandatory callback) — consumer screen only implements if custom init needed.
- Verification plan (blocked by Editor lock):
  - **Headless EditMode tests**: 25 total (22 + 3 new). `Unity -batchmode ...` blocked by user's running Editor. Manual run via Test Runner owed.
  - **Editor Press Play**: UIStep runs after LocalizationStep, 4 layer canvases appear in DontDestroyOnLoad hierarchy, bootstrap completes.
  - **Manual checklist** (append to `docs/testing/manual-checklist.md`): loading screen progress 0→1, safe area on iOS/Android, LocalizedText on locale change, toast FIFO.
- Resume hint: after Phase 3 merges to `main`, Phase 4 is Gameplay scaffolding (state machine, level lifecycle, domain events). Branch `phase-4-gameplay`. No UI phase further required unless consumer feeds back bugs or design feedback during their manual testing.

---
