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

### Round 2 patches (2026-04-30)

- 8 bugs fixed across UIService, PopupHandle, ToastQueue, PopupStack (IL2CPP compat, double-dispose, raycasting, exception leaks, backdrop sort drift).
- Commits:
  1. `fix(ui): replace dynamic with IPopupHandle for IL2CPP compat` — IPopupHandle interface added to PopupHandle.cs; PopupHandle<TResult> implements it. UIService._activePopups changed from `Stack<(..., dynamic, ...)>` to `Stack<(..., IPopupHandle, ...)>`. PopAsync calls `handle.Cancel()` directly without `dynamic` dispatch (Bug 15).
  2. `fix(ui): remove fictional Debug.LogDebug` — ToastQueue.cs line 102 changed `Debug.LogDebug` → `Debug.Log` (Bug 16).
  3. `fix(ui): remove double-dispose of prefab handles` — _loadedHandles field removed; per-push `prefabHandle?.Dispose()` in finally block is the only dispose path (Bug 17).
  4. `fix(ui): add GraphicRaycaster to backdrop Canvas` — CreateBackdrop now adds `GraphicRaycaster` component after `Canvas` to enable raycasting on override-sorting sub-canvas (Bug 18).
  5. `fix(ui): pop _activePopups on OnOpenAsync exception` — OnOpenAsync catch block now pops both _activePopups and popup-stack to prevent leaked entries (Bug 19).
  6. `fix(ui): clarify PopupClosed publish path in cancellation` — Added comment at PushAsync line 165 explaining PopupClosed is published by PopAsync, not re-published on handle exception (Bug 20).
  7. `refactor(ui): use PeekNextSortOrder to avoid backdrop sort drift` — PopupStack.PeekNextSortOrder() added; UIService.CreateBackdrop call site now uses it instead of duplicating formula (Bug 21).
  8. `refactor(ui): remove unused System.Linq` — Removed unused `using System.Linq` from UIService.cs (Bug 22).
- Verification: compile checked by diff inspection (IL2CPP compat via interface dispatch, no more fictional APIs, no more dynamic keyword, consistent sort order formula, proper exception cleanup, no double-dispose). Asmdef references verified; all new types internal-scoped except IPopupHandle (public for interface impl). Tests remain unchanged (PopupStackTests, ToastQueueTests, UITransitionInterruptionTests expected to pass).

### Round 3 — Editor compile fixes (2026-04-30)

User Editor surfaced multiple compile errors after round 2 patches. Each fixed in turn:

- `record struct` in `UIEvents.cs` requires C# 10; Unity 6 LTS uses C# 9. Converted to plain `readonly struct` with explicit ctor (commit `8d24457`).
- `LoadingScreenView` / `LocalizedText` referenced non-existent `R3.IDisposable`; R3 returns `System.IDisposable`. Added `using System; using R3;`. `PopupBase.CurrentData` had `protected get; protected set;` — CS0273 (setter must be more restrictive than property); changed to `private set` (commit `e0eed6a`).
- `UIServiceInstaller` used Reflex's generic `RegisterType<T>().As<I>()` API which doesn't exist in this Reflex version; switched to `RegisterType(typeof(UIService), new[] { typeof(IUIService) }, ...)` matching `AudioServiceInstaller`. `UITransitions` called `BindToCanvasGroupAlpha` (fictional); correct LitMotion uGUI extension is `BindToAlpha(CanvasGroup)` (verified in package source `LitMotionUGUIExtensions.cs:146`). `PopupStack._queue` was `Queue<PopupEntry>` but methods enqueued raw `GameObject`; aligned to `Queue<GameObject>`. `Object` ambiguous between `UnityEngine.Object` and `System.Object` in UIService/ScreenManager/ToastQueue; added `using Object = UnityEngine.Object` alias (commit `8d8cb92`).
- Tests: missing `using UnityEngine.TestTools;` for `[UnityTest]`. `MockAssetService` used `Task<T>` with `where T : class`; rewritten to `UniTask<T>` with `where T : UnityEngine.Object` matching `IAssetService`; added missing `PreloadAsync(IReadOnlyList<string>, IProgress<float>, CancellationToken)` (commits `73cad0a`, `6942892`).
- `UITransitionInterruptionTests.SequentialTransitions_AfterCancellation_Works` failed at runtime: LitMotion's `ToUniTask` awaits PlayerLoop Update ticks; EditMode does not tick the default Update timing, so the second (non-cancelled) tween never completes. Marked `[Ignore("Requires PlayMode")]`; PlayMode coverage moved to manual checklist (commit `2f1240a`).

### Round 4 — UIRoot refactor (2026-04-30, commit `0bd13a3`)

User direction: framework should not init UI in code. Consumer authors UI hierarchy in their own scene and points a `UIRoot` MonoBehaviour at the four layer Transforms; `Bootstrap.unity` stays minimal (splash-only).

- Deleted `LayerCanvas.cs` and `UIStep.cs`. Removed UIStep from `ProjectScopeInstaller.steps`.
- `IUIService`: removed `InitializeAsync`; added `AttachRoot(IReadOnlyDictionary<UiLayer, Transform>)` and `DetachRoot()`.
- `UIService`: layer Transforms populated by `AttachRoot`; `ScreenManager` + `ToastQueue` instantiated inside `AttachRoot`. All `Push/Pop/ShowScreen` guarded by `EnsureRootAttached()` → throws `InvalidOperationException` with explicit fix instructions if no root. `ShowToast` warns + drops message (fire-and-forget API stays no-throw).
- New `UIRoot.cs`: 4 SerializeField Transforms (Hud/Popup/Overlay/System), `[Inject] IUIService`. `OnEnable` builds layer dict and calls `AttachRoot`; `OnDisable` calls `DetachRoot`. Emits clear console errors if Reflex didn't inject (scene loaded outside container scope).
- Docs: new `docs/ui/ui-root.md` with step-by-step scene-setup recipe (8-step happy path, troubleshooting table, design rationale). `popup-stack.md` / `loading-screen.md` / `toast.md` updated to call out the UIRoot prerequisite. `CLAUDE.md` "Mock-first exceptions" entry rewritten to reflect consumer-owned root pattern.

---

## Phase 4 — 2026-04-30 (merged to `main`)

- Branch: `phase-4-gameplay` (4 commits; not yet merged to `main`)
- Files touched:
  - Runtime (new, under `Assets/_Project/Scripts/Runtime/Gameplay/`):
    - `IGameState.cs` — interface: `EnterAsync(ct)`, `ExitAsync(ct)`, `Tick(deltaTime)`.
    - `IGameStateMachine.cs` — interface: `CurrentState`, `Observable<IGameState> OnStateChanged`, `ChangeStateAsync`.
    - `GameStateMachine.cs` — flat state machine; sequential ExitAsync→EnterAsync→OnNext; rejects null, same-instance re-entry, and concurrent transitions.
    - `ILevelDefinition.cs` — abstract `ScriptableObject` (Id, DisplayName, AddressablePrefabKey).
    - `LevelLoader.cs` — `LoadLevelAsync(key, ct)` returns `(GameObject Instance, IAssetHandle<GameObject> Handle)`; caller disposes handle.
    - `GameplayServiceInstaller.cs` — Reflex installer; binds `GameStateMachine` (as `IGameStateMachine`) and `LevelLoader` (self-binding), both Singleton/Lazy.
    - `Events/{LevelStarted, LevelCompleted, LevelFailed, LevelRestarted, LevelExited}.cs` — `readonly struct` event POCOs (namespace `Zero.Gameplay.Events`); explicit ctor (NOT `record struct` — Unity 6 LTS is C# 9).
    - `States/{BootState, MenuState, PlayState, PauseState, ResultState}.cs` — minimal sample shells, EnterAsync logs only; consumer replaces.
  - Bootstrap: `ProjectScopeInstaller.cs` — `using Zero.Gameplay;` + `GameplayServiceInstaller.Install(builder)`. **No bootstrap step** — state machine has no async init.
  - Asmdef: `Zero.Tests.EditMode.asmdef` adds `Zero.Gameplay` reference. `Zero.Gameplay.asmdef` references unchanged (already had Core/Infrastructure/Events/UniTask/R3/Reflex/LitMotion; **no Zero.UI / no Zero.Meta** — peer rule preserved).
  - Tests (3 new EditMode suites, ~14 cases):
    - `GameStateMachineTests.cs` — initial null state, first transition sets state, transition calls Exit-before-Enter, re-entry of same instance throws, null state throws, OnStateChanged fires once per change, cancellation propagates, **concurrent ChangeStateAsync throws** (uses gated `SlowEnterState` to hold mid-transition).
    - `LevelLifecycleEventsTests.cs` — round-trip for each of 5 level events through real `R3EventBus`.
    - `GameplayUiDecouplingTests.cs` — integration "decoupling test" per PLAN §3 Phase 4 acceptance: real `R3EventBus`, fake "UI" subscriber receives `LevelCompleted`/`LevelFailed`/`LevelStarted` published by fake "Gameplay" caller; multi-subscriber fan-out; "publish-without-subscribers" silent path. The literal asmdef-grep check is verified manually (`grep "Zero.UI\|Zero.Meta" Zero.Gameplay.asmdef` → empty).
  - Docs: `docs/gameplay/state-machine.md` and `docs/gameplay/level-loading.md` (fixed format).
- Workflow: spawned **Haiku junior-dev** subagent (general-purpose, worktree isolation) to implement, then **Opus lead review** in main session — 5 bugs found and patched in 3 commits.
- Production bugs the review caught (would have shipped silently otherwise):
  1. **All 3 test files missed `using System.Collections;`** — `[UnityTest]` returns `IEnumerator`; without the using, every Phase 4 test file fails to compile. Same shape as prior-phase test scaffolding bug.
  2. **`LevelLoader.cs` `Object.Instantiate` ambiguous** — `using System` + `using UnityEngine` collide on `Object` (CS0104). Same footgun as Phase 3 round 3; PITFALLS entry already existed but new code skipped the alias. Fix: `using Object = UnityEngine.Object;`.
  3. **`LevelLoader` had `IAssetService` ctor dep but was never registered with Reflex** — would `IsRegistered = false` from the container; consumer trying to inject `LevelLoader` would get a Reflex resolution exception. Fix: self-binding `RegisterType(typeof(LevelLoader), new[] { typeof(LevelLoader) }, ...)` in `GameplayServiceInstaller`.
  4. **`ChangeStateAsync` did not call `ct.ThrowIfCancellationRequested()` at entry** — when called with pre-cancelled token AND no current state AND not already transitioning, the method took no real await path and never observed the token; cancellation was silently ignored. The CancellationPropagates test would have failed at runtime. Fix: explicit `ct.ThrowIfCancellationRequested()` after `ThrowIfDisposed()`.
  5. **Concurrent-transition "queue" was racy** — `while (_isTransitioning) await UniTask.Yield(ct)` busy-wait with non-atomic `_isTransitioning = true` meant when transition N ended, all queued awaiters woke and raced for the slot, leaving final state undefined. PLAN §3 said "queued or rejected (decide)"; picked **reject** as the minimal-template choice (real queue adds hidden ordering surprises). Replaced racy `SequentialCalls_NoReentrancy` test with `ConcurrentCall_Throws` using a gated `SlowEnterState`. Updated `IGameStateMachine` xmldoc.
- Verification:
  - **Static**: every API signature in the new code re-read against actual source files in `Assets/_Project/Scripts/Runtime/Core/Interfaces/*.cs` (IAssetService, IEventBus) and existing installers (`EventsServiceInstaller`, `AudioServiceInstaller`) for Reflex registration shape. Compile-clean by inspection.
  - **Headless EditMode test run**: not attempted — user's Editor was running on prior phases too. User to run via Test Runner before merge.
  - **`.meta` files**: not generated yet for new `.cs`/`.asmdef` files (Unity creates on next Editor open). User commits resulting metas after first Editor open. Same situation as Phase 3 (UIRoot.cs.meta was committed post-merge).
- Resume hint: Phase 4 merged after user-side Editor verification (console clean, Test Runner green). Next: Phase 5 split into 5a (runtime + tests) and 5b (docs).

---

## Phase 5a — 2026-04-30 (merged to `main`)

- Branch: `phase-5a-liveops-devtools` (8 commits)
- Files touched:
  - Runtime VersionCheck: `IVersionCheckService.cs` (Core; `VersionStatus` enum + `VersionCheckResult` readonly struct), `Zero.Services.VersionCheck/{VersionCheckService.cs, VersionCheckServiceInstaller.cs, Zero.Services.VersionCheck.asmdef}` — semver compare (3-part major.minor.patch; malformed → warn+Ok), bus order: maintenance > ForceUpdate (local<min) > SoftUpdate (local<recommended) > Ok. Service registered via `RegisterFactory` because the ctor takes a `string localVersion` Reflex can't auto-resolve.
  - Bootstrap step: `Steps/VersionCheckStep.cs` (non-critical, runs after RemoteConfigStep). Registered + ordered in `ProjectScopeInstaller`.
  - Asmdef wiring: `Zero.Bootstrap.asmdef` adds `Zero.Services.VersionCheck`. `Zero.Tests.EditMode.asmdef` adds `Zero.Services.VersionCheck` + `Zero.DevTools` + `Zero.Services.RemoteConfig`.
  - Runtime DevTools: `Zero.DevTools/{IConsoleCommand.cs, ConsoleCommandAttribute.cs, BuiltInCommands.cs (4 commands), CheatConsole.cs, FpsOverlay.cs, DevToolsBootstrap.cs, Zero.DevTools.asmdef}`. Asmdef gated `defineConstraints: ["UNITY_EDITOR || DEVELOPMENT_BUILD"]`. Command discovery via reflection scan of all loaded assemblies; instantiation via `Container.Construct(Type)` (not `Resolve` — commands aren't registered as contracts) with fallback to `Activator.CreateInstance`. Cheat console toggle: tilde key + 4-finger touch via `Touchscreen.current.touches` (new Input System; legacy `Input.touchCount` would throw because project is Input-System-only). FPS overlay toggle: F2.
  - Built-in commands: `loc set <locale>`, `version check`, `fps show/hide`, `save reset` (stub — logs "extend ISaveService per-game"; ISaveService has `Delete(key)` but no wholesale reset by design).
  - Spawn: `DevToolsBootstrap.Initialize` via `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` creates `[Zero.DevTools]` GameObject + `DontDestroyOnLoad`. Whole method ifdef-gated as belt-and-suspenders alongside the asmdef constraint.
  - Tests: `VersionCheckServiceTests.cs` (9 cases — full decision matrix with injected local version "1.0.0"), `ConsoleCommandParserTests.cs` (8 cases — greedy two-word match, args splitting, unknown/empty/whitespace input).
- Workflow: spawned **Haiku junior-dev** in worktree, then 3 review rounds in main session — round 1 caught 11 bugs, round 2 silenced 2 CS1998 warnings, round 3 fixed semver-test failure caused by `Application.version` template default ("0.1" 2-part).
- Production bugs the review caught (would have shipped silently):
  1. **`SaveResetCommand` called `ISaveService.ResetAsync(ct)`** — doesn't exist on the interface (Save has `Delete(key)` only, no wholesale reset by design). Replaced impl with a "stub — extend per-game" warn.
  2. **`VersionCheckService.ParsedVersion` used C# 9 `init;` accessors** — needs `IsExternalInit` polyfill not present in Unity 6 .NET Standard 2.1. Same shape as Phase 3 `record struct` bug. Converted to readonly props with explicit ctor.
  3. **`CheatConsole` referenced `ContainerScope.Root`** — doesn't exist in Reflex. Correct API is `Container.RootContainer` (verified against package source under `Library/PackageCache/com.gustavopsantos.reflex@*`).
  4. **`CheatConsole` called `container.Resolve(commandType)`** — but command types aren't registered as Reflex contracts. Correct API is `container.Construct(Type)` which does ctor injection without registration; resolved deps still come from the container.
  5. **`CheatConsole` used legacy `Input.touchCount` / `Input.GetTouch(0)`** — throws when "Active Input Handling" is set to Input System Package only (this project's setting per Phase 2). Switched to `Touchscreen.current.touches`.
  6. **`Zero.DevTools.asmdef` missed `Unity.InputSystem` reference** — cascading from #5.
  7. **`VersionCheckServiceTests` missed `using UnityEngine.TestTools;`** — `[UnityTest]` unresolved. Same shape as Phase 4 round 2 footgun.
  8. **Test field declared as `private VersionCheckService.VersionCheckService _service;`** — namespace + class share name, so `VersionCheckService` is both the namespace and the class; the dotted form tries to resolve a nested static type that doesn't exist. Stripped to single name.
  9. **`MockLogService.IsEnabled` was get-only** — interface declares both accessors. Same shape as Phase 4 round 2 stub mismatch. Made `{ get; set; }`.
  10. **`ConsoleCommandParserTests.ParseCommand_NoArgsForCommand_EmptyArgsArray` assertion was wrong** — the command "fps" IS registered, so the parser returns it; corrected the assertion.
  11. **Both new asmdefs referenced sibling asmdefs via fabricated GUID strings** — Unity would fail to resolve. Repo convention is plain string names (e.g. "Zero.Core"). Switched.
  12. **Round 2 (CS1998 warnings):** `CheckAsync` was `async UniTask<...>` with no real await — synchronous compute path. Removed `async`, wrapped each return with `UniTask.FromResult`. Test cancellation lambda `async () => await _service.CheckAsync(...)` had no real outer await; switched to `() => _service.CheckAsync(...).AsTask()` + trailing `await UniTask.CompletedTask;`.
  13. **Round 3 (test runtime failure):** ForceUpdate / SoftUpdate tests asserted ForceUpdate/SoftUpdate but got Ok. Cause: service used `Application.version` directly; default template ProductVersion is "0.1" (2-part) which fails 3-part semver parse → warn + Ok regardless of remote min_version. Fix: ctor takes `string localVersion`; production binding via `RegisterFactory` supplies `Application.version`; tests inject `"1.0.0"`.
- Verification: console clean, Test Runner green (16 new + 39 prior = 55 EditMode cases). User confirmed.
- Resume hint: Phase 5b is docs-only.

---

## Phase 5b — 2026-04-30 (merged to `main`) — TEMPLATE COMPLETE

- Branch: `phase-5b-docs` (3 commits)
- Files created (21 total):
  - **Repo-level (5):** `README.md` (rewritten — stack table, ASCII architecture diagram, 7-step Quick Start incl. ZeroSecrets.asset setup, phase status table, full doc index), `README.vi.md` (Vietnamese pitch + Quick Start per PLAN §7 #9), `LICENSE` (MIT, copyright 2026 Bao Tran), `CONTRIBUTING.md` (service-add recipe, mock SDK extension flow, test conventions, phase workflow, conventional-commit style), `CHANGELOG.md` (Unreleased v0 section grouped by phase).
  - **Architecture (1):** `docs/architecture/asmdef-graph.md` — Mermaid DAG of all 28 runtime asmdefs + 2 test asmdefs, tier breakdown (Core / Infra / Services / Peers / Composition root / Aux DevTools), extension recipe.
  - **Service docs (2):** `docs/services/version-check.md` (factory binding rationale, semver edge cases, status routing), `docs/services/time.md` (stub-only template default + server/NTP extension recipe).
  - **Live-Ops (2):** `docs/liveops/version-check.md` (consumer LiveOpsGate pattern, soft-update friction levels, re-check on app foreground), `docs/liveops/addressables-remote.md` (CDN setup, RemoteLoadPath config, content update flow — consumer-supplied CDN, no hardcoded URL).
  - **DevTools (2):** `docs/dev/cheat-console.md` (built-in commands, custom-command recipe, save-reset override pattern), `docs/dev/fps-overlay.md` (F2 toggle, what it shows, extension hooks for battery/thermal).
  - **Mock SDK extension recipes (8):** `docs/services/{crashlytics,consent,remote-config,analytics,attribution,ads,iap,receipt-validator}.md`. Each: fixed-format Overview / Public API / Mock behavior / Extension Points (with installer-line swap to Firebase / Google UMP / AppLovin / Unity IAP / etc.) / Examples / Known Limitations / Design Rationale.
  - **Meta recipes (1):** `docs/meta/recipes.md` — pseudo-code patterns for wallet, progression, reward grant, daily login, A/B variants, plus a per-game `MetaInstaller` wiring example. Per PLAN §2.4 no impl ships.
- Workflow: spawned a Haiku junior subagent first; Haiku hit the model rate limit before writing any file. Switched to writing in main session directly (lower risk for prose-only work; reviewer skim covers fact-checking).
- Verification: every interface signature, installer file path, and SDK API name in the docs was cross-checked against the actual source under `Assets/_Project/Scripts/Runtime/Core/Interfaces/` and `Library/PackageCache/`. Mock + interface shapes verified for all 8 SDK recipes.
- Things to watch: SDK-specific API examples (Firebase / AppLovin / Unity IAP) are written from documentation knowledge, not in-repo source — those vendors update APIs frequently. Treat the recipe code as a starting point; consumers should re-verify against current SDK docs when they actually wire a real adapter.
- Resume hint: **template is complete.** All five build phases (1a / 1b / 2 / 3 / 4 / 5a / 5b) are merged to `main`. `docs/dev/PLAN.md` §3 has no Phase 6. Future work is consumer fork (per-game meta, real SDK adapters, genre-specific gameplay) — not template work.

### Phase 4 round 2 — Editor compile fixes

User Editor compile surfaced 3 errors + 1 warning after the round-1 review patches:

- `LevelLifecycleEventsTests` carried a `StubLogService` whose method shape didn't match `ILogService` (`IsEnabled` get-only, `Debug`/`Warning` instead of `Warn`, missing `Error(Exception, string)`). Stub was unused (R3EventBus has parameterless ctor). Deleted entirely.
- `LevelLifecycleEventsTests` and `GameplayUiDecouplingTests` missed `using R3;` — `Observable<T>.Subscribe(Action<T>)` is a R3 extension; without the using, the compiler picked `Subscribe(Observer<T>)` overload and rejected every lambda subscription with CS1660. Three test files all need this — promote to PITFALLS.
- `GameStateMachineTests.TestState.{EnterCalled, ExitCalled}` had `private set;` but the test "Transition_CallsExitBeforeEnter" reset them between transitions. Made setters public (test-internal type).
- `ChangeStateAsync_NullState_Throws` had a stray `await Assert.ThrowsAsync<...>` — NUnit's `ThrowsAsync` returns the exception synchronously; awaiting it triggered `EnumeratorAsyncExtensions.GetAwaiter<ArgumentNullException>` resolution failure (CS0311). Dropped the await.
- CS1998 (async-without-await) silenced via trailing `await UniTask.CompletedTask;` in the two tests whose only async surface was a synchronous `Assert.ThrowsAsync`.

Editor verification after round 2: console clean, Test Runner green. 18 `.meta` files for Phase 4 .cs/.asmdef were generated by Editor and committed alongside.

---

## Post-review cleanup — 2026-05-17 (`chore/post-review-cleanup`) — v0.2.3

Triggered by a full-project review (three agents: asmdef-boundary-reviewer, pitfalls-guard, unity-lead). Architecture verdict PASS; asmdef WARN (phantom R3 ref); pitfalls FAIL. Three independent commits:

- **A — pending deletion finalized.** The 5 `Runtime/Gameplay/States/*.cs` shells (`Boot/Menu/Play/Pause/Result`, 29-line `Debug.Log` `IGameState` stubs) were staged-for-deletion in the working tree. Verified intentional + safe: `.cs`+`.cs.meta`+`States.meta` deleted together (no orphan metas), zero references in `Runtime/`/`Tests/`/scenes/prefabs, `GameStateMachine`+`LevelLoader`+lifecycle events intact, Phase 4 tests use private gated test states (not the samples). Committed the deletion. Rationale: example shells in shipped `Runtime/` blur the genre-agnostic, consumer-owns-states boundary (`CLAUDE.md` Gameplay principle).
- **B — P1 pitfalls.** (1) `Object.Destroy` reachable from EditMode without guard in `UIService` (×7), `ScreenManager` (×2), `ToastQueue` (×1) + unguarded `await UniTask.Yield()` in `ScreenManager.UnloadScreenAsync` → added `Zero.UI.UiObjects.SafeDestroy` (internal, mirrors `UnityPoolService.SafeDestroy`; not a cross-asmdef ref into `Zero.Services.Pool`). (2) `AudioMixerService.PlayMusicAsync`/`PlaySfxAsync` missing `HasKeyAsync` pre-check before `LoadAsync<AudioClip>` (red `InvalidKeyException` escapes try/catch) → guarded per `InitializeAsync` pattern. (3) **Constitution drift:** `docs/services/{localization,pool,audio,version-check}.md` told readers to "subclass / override / extend" `sealed` impls or a `readonly struct` — directly violates the sealed-services principle in `CLAUDE.md`. Rewritten to the binding-swap / decorator model. Added behavior-anchored EditMode regression test `ScreenManagerEditModeDestroyTests` (fail-iff: old code throws "Destroy may not be called from edit mode"). **Same-shape sweep** (CLAUDE.md debug philosophy): the unguarded-PlayerLoop-await footgun also hit `UIService.PopAsync` (`await UniTask.Yield`) and `ToastQueue.ProcessQueueAsync` (`await UniTask.Delay`) — both guarded with `Application.isPlaying` in the same pass. Test coverage: the Destroy path is regression-tested; the AudioMixer `HasKeyAsync` pre-check and the Yield/Delay guards are static-only (no PlayMode harness for them in-template).
- **C — chore.** Removed inert `"R3"` from `references[]` in 18 runtime asmdefs. R3 is a NuGetForUnity DLL; with `overrideReferences:false` it's auto-included, so the asmdef entry was silently ignored and only misled reviewers into thinking asmdef-level protection existed. WARN-2 (Notification→ISaveService) intentionally NOT changed — `ISaveService` lives in `Zero.Core` which is already referenced; the dep is interface-only via DI.
- Verification: static greps green (no `Object.Destroy(` outside `UiObjects.cs` in `Runtime/UI/`, no `"R3"` in runtime asmdefs, no old state-shell refs). **Editor verification (Play + Test Runner EditMode/PlayMode) pending — Claude cannot open Unity; user to confirm.**
- Resume hint: after user confirms Editor green, this branch is mergeable. No follow-up phase.
