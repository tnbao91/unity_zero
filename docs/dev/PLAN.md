# Plan — Unity Zero v2: Opensource Hybrid Casual Template

## 1. Context

Repo `/Users/baotran/Desktop/Projects/unity_zero` hiện là Unity 6 LTS template cho hybrid casual/puzzle games. Stack đã chốt (locked, không thay): Reflex DI, UniTask, R3, LitMotion, Newtonsoft.Json, ZString, Addressables, Unity Input System.

**Trạng thái hiện tại:**
- ~22 asmdefs với dependency direction nghiêm ngặt (Core → Infrastructure → Services → Bootstrap).
- Production-ready (giữ nguyên): `LogService`, `DeviceProfileService`, `EncryptedJsonSaveService`, `AddressableAssetService`, `AddressableSceneService`, `ReflexPoolService`, `DefaultAdPlacementService`.
- Mock-only (intentional template default): Crashlytics, Consent, RemoteConfig, Analytics, Attribution, Ads, IAP, Audio, Notification, Input. Plus `StubReceiptValidator`, `StubTimeService`.
- **3 layer hoàn toàn trống**: `Zero.Gameplay`, `Zero.UI`, `Zero.Meta` (chỉ có asmdef, 0 code).
- Tests + Editor tools: empty asmdefs.

**Mục tiêu user đã chốt:**
- **Template-first, generic cho mọi genre.** Không build demo game.
- **Opensource quality** — Codex sẽ review nên design rationale phải defendable.
- **Docs MD per module** — mỗi hạng mục có hướng dẫn + design notes riêng.
- **Mức tối giản** — không over-engineer, chỉ cover phần đa số genres dùng chung.
- **Prefer Unity-default packages** — wrap official packages (`com.unity.localization 1.5.11`, `com.unity.mobile.notifications 2.4.3`, `UnityEngine.Pool.ObjectPool`, `com.unity.purchasing 5.2.1`) thay vì custom impl.
- **Meta out of scope** — không ship Wallet/Progression/Reward generic. Hybrid casual và puzzle có meta loop khác nhau → consumer build per-game; recipes only.

**Outcome mong muốn:** `unity_zero` thành opensource template chỉnh chu, ai pull về cũng có thể bắt đầu build hybrid casual/puzzle MVP trong vài ngày bằng cách extend các extension points đã document, không phải refactor framework.

---

## 2. Architectural Decisions (defendable)

### 2.1 Asmdef restructure — peer Gameplay/Meta/UI via event bus

**Vấn đề hiện tại:** `Zero.Gameplay.asmdef` reference cả `Zero.Meta` và `Zero.UI` → "level complete → grant currency → show popup" thành direct call chain. Đây là blocker cho multi-genre reuse: mỗi genre có meta/UI khác nhau, không thể đóng cứng chiều phụ thuộc.

**Decision:** Tách 3 layer thành **peers**, giao tiếp qua `IEventBus`. DAG vẫn acyclic vì `Zero.Bootstrap` (composition root) là layer duy nhất reference cả 3.

```
Zero.Core (interfaces, POCOs, domain events cross-cutting)
  ↑
Zero.Infrastructure
  ↑
Zero.Services.<Name>  ← bao gồm Zero.Services.Events, Zero.Services.Localization (mới)
  ↑          ↑          ↑
Zero.UI   Zero.Meta   Zero.Gameplay   ← peers, talk via IEventBus
        ↘     ↓     ↙
      Zero.Bootstrap
```

### 2.2 Event bus = service convention, không tạo tier mới

`IEventBus` thêm vào `Zero.Core`. Implementation `R3EventBus` trong **`Zero.Services.Events.asmdef` (mới)**, theo đúng convention service hiện có (installer + bootstrap step nếu cần). Không invent layer mới.

```csharp
public interface IEventBus
{
    Observable<T> On<T>();
    void Publish<T>(T evt);
}
```

Implementation: lazy `Dictionary<Type, object>` storing `Subject<T>` instances cast on access (R3 đã có sẵn trong stack). **Tránh `Dictionary<Type, Subject<object>>`** — kiểu đó box value-type events vào `object` slot. Bus stores typed `Subject<T>` and casts at call site:
```csharp
private readonly Dictionary<Type, object> _subjects = new();
public Observable<T> On<T>() {
    if (!_subjects.TryGetValue(typeof(T), out var s)) {
        s = new Subject<T>();
        _subjects[typeof(T)] = s;
    }
    return ((Subject<T>)s);
}
```

Event POCOs (record struct preferred cho zero-alloc):
- Cross-cutting events (`AppPaused`, `AppQuitting`) → `Zero.Core`
- Domain-local events (`LevelCompleted`, `LevelFailed`, ...) → asmdef sở hữu (Gameplay).
- Meta events (`CurrencyChanged`, `RewardEarned`, ...) → consumer asmdef (vì Meta out of scope của template).

**Defending choice:** "Why not direct R3 Subject in each service?" Vì cross-asmdef communication cần một bus thống nhất (không thể inject cụ thể `Subject<LevelCompleted>` mà subscriber asmdef không tham chiếu publisher). Bus type-keyed cho strong typing + zero direct coupling.

### 2.3 Generic Gameplay scaffolding (`Zero.Gameplay`)

Minimum-but-complete. Genre-specific (grid/runner/idle/merge/match-3) **không** thuộc template — consumer extend.

- `IGameStateMachine` — flat states (HSM để v2 nếu user quyết).
- `IGameState` — `EnterAsync(ct)`, `ExitAsync(ct)`, `Tick(deltaTime)`.
- `ILevelDefinition` — abstract ScriptableObject base (Id, DisplayName, AddressablePrefabKey).
- `LevelLoader` — uses `IAssetService` để load level prefab.
- Lifecycle events qua bus: `LevelStarted`, `LevelCompleted`, `LevelFailed`, `LevelRestarted`, `LevelExited`.

### 2.4 Meta layer — OUT OF SCOPE

**Decision:** template không ship `WalletService`/`ProgressionService`/`RewardService`/`VariantResolver`/`DailyLoginService` generic. Hybrid casual và puzzle có meta loop khác nhau (currency model, progression curve, reward shape) → generic Meta là sludge mà mọi consumer rewrite. Để Meta cho consumer build per-game.

- `Zero.Meta.asmdef` giữ làm **empty placeholder** cho consumer code (deps: `Zero.Core`, `Zero.Infrastructure`, `Zero.Services.Events`).
- Không có `Zero.Meta` interfaces trong `Zero.Core`.
- `docs/meta/recipes.md` provides reference patterns (pseudo-code: how to wire a wallet via `ISaveService` + `IEventBus`, how to expose variants via `IRemoteConfigService`) — recipes only, no impl ships.

**Defending choice:** opensource template philosophy = "minimal infra, opinion-free meta". Codex review point of contention sẽ là "tại sao không có Wallet?" → answer: scope-bounded, recipes provided.

### 2.5 Generic UI scaffolding (`Zero.UI`)

`UIService` impl của `IUIService`:
- Popup stack: push/pop/queue/replace, modal mask, layer-aware.
- `UiLayer` enum: Hud / Popup / Overlay / System (4 Canvas riêng, sort order 100/200/300/400).
- Transitions LitMotion-backed (fade/slide/scale).
- `LoadingScreenView` — placed directly in `Bootstrap.unity`, subscribes to **`IBootstrapProgressReporter`** (interface trong `Zero.Core`, impl trong `Zero.Infrastructure`). View NEVER resolves `BootstrapPipeline` directly — pipeline writes to reporter, view reads from reporter. One-way dependency, no Lazy-singleton resolution race. **Deliberate exception** to "all UI through IUIService" vì UIService init muộn hơn loading screen.
- `SafeAreaFitter` MonoBehaviour cho notch.
- `ToastQueue` — short text, FIFO, auto-dismiss.
- `LocalizedText` MonoBehaviour subscribe `IL10nService.OnLocaleChanged`.

UI prefabs ship qua Addressables, key convention: `ui/popup/<name>`, `ui/screen/<name>`.

### 2.6 Localization — wrap `com.unity.localization` (đã có trong manifest 1.5.11)

**Không build custom StringTable.** Unity Localization Package đã ship `LocalizationSettings`, `LocalizedString`, `StringTable`, `Locale` switching, Smart Strings, persistence — cover hơn xa custom impl.

`IL10nService` interface giữ trong `Zero.Core` cho DI test seam:
```csharp
string Get(string key, params object[] args);
Observable<Locale> OnLocaleChanged;
UniTask SetLocaleAsync(LocaleIdentifier locale, CancellationToken ct);
Locale CurrentLocale { get; }
```

Real impl `UnityLocalizationService` thin wrapper:
- `Get(key, args)` → `LocalizationSettings.StringDatabase.GetLocalizedString(tableRef, key, args)`
- `OnLocaleChanged` → R3 wrap `LocalizationSettings.OnSelectedLocaleChanged`
- `SetLocaleAsync` → `LocalizationSettings.SelectedLocale = locale` + `await SelectedLocaleAsync.ToUniTask()`

`LocalizationStep`:
- Await `LocalizationSettings.InitializationOperation.ToUniTask()` để guarantee init xong trước UI dùng.

**Key style:** namespaced (`ui.popup.win.title`).
**Mock impl:** `MockLocalizationService` dictionary-backed cho EditMode tests.
**No CSV import script ships** — Unity Localization có Editor window built-in (`Window → Asset Management → Localization Tables`); pattern document trong `docs/services/localization.md`.

### 2.6b Pool refactor — use `UnityEngine.Pool.ObjectPool<T>` internally

**Vấn đề hiện tại:** `ReflexPoolService` (`Assets/_Project/Scripts/Runtime/Services/Pool/ReflexPoolService.cs`) tự build `Stack<GameObject>` cho inactive store, không dùng `UnityEngine.Pool.ObjectPool` (built-in từ Unity 2021+). Tên "Reflex" cũng misleading — nó không liên quan Reflex DI's pool framework, chỉ là service trong project Reflex-bound.

**Decision:** rename `ReflexPoolService` → `UnityPoolService`. Internal storage swap sang `UnityEngine.Pool.ObjectPool<GameObject>` với:
- `actionOnGet` — `SetActive(true)` + reset transform
- `actionOnRelease` — `SetActive(false)` + reparent to `[Zero.Pools]` root
- `actionOnDestroy` — `Object.Destroy(go)`
- `collectionCheck: true` trong Editor (debug double-release), `false` trong player builds (perf)
- `maxSize` configurable per-pool

**`IPoolService` interface giữ nguyên** — không breaking change cho consumer. Refactor là internal-only.

**Defending choice:** Codex review sẽ flag "why hand-rolled pool" → answer: original was prior-art before this refactor; v2 uses Unity-built-in để thừa hưởng `collectionCheck` + `maxSize` + battle-tested code.

### 2.7 Real Input service (Phase 2)

Expand `IInputService` ở Phase 1 (interface stable trước khi consumer extend), implement ở Phase 2:
```csharp
Observable<Vector2> OnPointerDown, OnPointerUp, OnTap, OnDrag;
Observable<SwipeInfo> OnSwipe;     // direction + magnitude + velocity
Observable<float> OnPinch;          // delta scale
Observable<Unit> OnEscape;          // Android back / Esc
```
Real impl `UnityInputService` wrap `InputSystem.EnhancedTouch` + `PlayerInput`, gesture detection (tap window 200ms, swipe threshold 50px) configurable.

### 2.8 Real Audio service (Phase 2)

`AudioMixerService`:
- Ship default `AudioMixer.mixer` asset với buses Master/Music/Sfx/Ui/Voice.
- AudioSource pool qua `IPoolService.GetPool<AudioSource>` (reuse — không tạo pool riêng).
- Bus volumes persisted via `ISaveService` keys `audio.bus.<name>`.
- Music crossfade dùng LitMotion volume tween.
- One-shot SFX via pooled source, auto-return on clip end.

### 2.8b Real Notification service (Phase 2) — wrap `com.unity.mobile.notifications` (đã có trong manifest 2.4.3)

**Decision:** thay `MockNotificationService` bằng `UnityMobileNotificationService` real impl wrap Unity Mobile Notifications package.

- iOS path: `iOSNotificationCenter.ScheduleNotification(...)`, request authorization on first schedule.
- Android path: `AndroidNotificationCenter.SendNotification(...)`, register channel on init.
- Cross-platform via `#if UNITY_IOS` / `#if UNITY_ANDROID` ifdef inside service; Editor path = no-op + log.
- `INotificationService` interface giữ nguyên — Mock vẫn dùng cho headless tests.
- `NotificationStep` thêm permission request flow (ask once, persist outcome via `ISaveService` key `notification.permission.requested`).

**Defending choice:** Mocking notifications cho hybrid casual không make sense — đây là feature core của retention loop, real impl ship được luôn vì Unity package handle multi-platform.

### 2.9 Live-ops skeleton (Phase 6)

`IVersionCheckService`:
- Compare `Application.version` vs `IRemoteConfigService.GetString("min_version")`.
- Compare against `maintenance_mode` flag.
- Emit `VersionCheckResult { Status: Ok | SoftUpdate | ForceUpdate | Maintenance }`.
- Bootstrap step gates app: nếu `ForceUpdate` hoặc `Maintenance` → block `GameLauncher`, show maintenance popup.

Addressables remote catalog config: document `RemoteLoadPath` setup trong `docs/liveops/addressables-remote.md`, **consumer cung cấp CDN URL** (không hardcode).

### 2.10 Cheat console / Debug overlay (Phase 6)

In-game console:
- Toggle: tilde key (PC) / 4-finger tap (mobile).
- Command registry `IConsoleCommand` + `ConsoleCommandAttribute` (auto-discover).
- Built-in commands: `wallet add <currency> <amount>`, `level goto <id>`, `save reset`, `time set <iso8601>`, `loc set <locale>`, `fps show/hide`.
- FPS overlay: simple Text + frame time graph.
- Wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.

### 2.11 Bootstrap improvements

- **Wire progress:** `BootstrapPipeline.RunAsync` line 31 đang pass `null` cho per-step progress. Chuyển sang `var slice = new Progress<float>(p => overallProgress?.Report((i + p) / _steps.Count));` để loading bar smooth.
- **Per-step timeout:** `IBootstrapStep` thêm `TimeSpan Timeout { get; }` (default 30s, network-bound steps override). Pipeline wraps mỗi step trong `cts.CancelAfter(step.Timeout)`.
- **Retry policy:** thêm `int MaxRetries { get; }` (default 1). Apply chỉ khi non-critical fail.
- **Reorder steps:** Move `SaveStep` lên ngay sau `DeviceProfileStep` (currently last at line 95). Lý do: nhiều service sau muốn đọc settings (audio volume, locale, consent state cached) khi swap real impl. Hiện tại Crashlytics đứng đầu vì critical — giữ nguyên. Order mới: Crashlytics → Log → DeviceProfile → Save → Asset → Consent → RemoteConfig → Analytics → Localization → Attribution → Ads → IAP → Audio → Time → Input → Notification → Events → VersionCheck → Pool.
- **`IBootstrapProgressReporter` thay vì expose Subject trên Pipeline:** interface trong `Zero.Core` exposes `Observable<float> Progress` + `Observable<string> CurrentStepName`. Impl `BootstrapProgressReporter` trong `Zero.Infrastructure` (Singleton). Pipeline injects + writes; LoadingScreen injects + reads. **Tránh resolution-timing puzzle** với Pipeline `Lifetime.Singleton, Resolution.Lazy` — view không resolve pipeline trực tiếp.

### 2.12 Save service hardening

**Decision:** ScriptableObject-based seed (gitignored).
- File `Assets/Resources/ZeroSecrets.asset` (gitignored), companion `ZeroSecrets.asset.example` checked-in với placeholder.
- `EncryptedJsonSaveService` constructor reads from Resources; if missing or matches example → log `CRITICAL` + `throw InvalidOperationException` trong non-Editor builds.
- Editor builds: warn loud nhưng không throw để dev iteration không bị chặn.
- Tài liệu `docs/services/save.md` ghi rõ: "client-side crypto chỉ chống casual editing; nếu economy/leaderboard quan trọng, phải có server validation".

**Giữ nguyên** behavior reset-to-empty on decrypt fail (line 72, 86) — defendable cho hybrid casual (single-slot, recoverable progress). Document option `IFailLoudHandler` cho consumer override nếu cần.

### 2.13 Tests + CI (Phase 1)

EditMode tests cho 4 thứ production-ready:
- `EncryptedJsonSaveService` — round-trip + tamper detection (flip 1 byte, expect HMAC fail) + **migration callback fired & migrated payload persisted** (write v0 file → load → assert `Migrate(...)` invoked → assert next save writes v1 envelope). Đây là extension point consumer dùng nhiều nhất, untested footgun.
- `ReflexPoolService` — get/release ordering, prewarm count, dispose cleanup.
- `BootstrapPipeline` — order preserved, IsCritical abort, non-critical swallow, progress reporting accurate, cancellation propagates, timeout fires, retry policy honored.
- `R3EventBus` — publish/subscribe/dispose, multiple subscribers, type isolation.

Mỗi phase sau add tests cho phần build trong phase đó (acceptance criteria).

**PlayMode test policy:** CI EditMode-only. Phase 2 ships real Input + Audio whose acceptance ("tap fires on device", "music crossfade smooth") cannot be asserted headless. Decision: **manual editor verification checklist** in `docs/testing/manual-checklist.md` — explicit per-feature steps + expected result, signed off before phase closes. PlayMode test job có thể add v2 nếu user muốn.

CI workflow `.github/workflows/tests.yml`:
- Trigger: push, pull_request.
- Job: `game-ci/unity-test-runner@v4`, EditMode only, Unity `6000.3.11f1`.
- Cache Library/ folder.
- README ghi rõ consumer phải set `UNITY_LICENSE` secret.

### 2.14 Documentation strategy — docs ship WITH each phase, not after

**Principle:** mỗi phase mà tạo public API mới phải ship doc cùng phase. Defer hết về Phase 6 = stale docs vs code drift sau 5 phases. Phase 6 chỉ còn cross-cutting (README, LICENSE, CONTRIBUTING, asmdef-graph diagram).

**Fixed format mỗi MD file:**
```
# <Module Name>
## Overview              -- 2-3 sentences
## Public API            -- interfaces + key signatures
## Extension Points      -- how to swap/extend
## Examples              -- code snippet, no UI screenshots
## Known Limitations     -- be honest
## Design Rationale      -- why this approach (the "memory")
```

**Doc language:** English primary (front door cho international audience). Optional `README.vi.md` tóm tắt tiếng Việt (chỉ pitch + Quick Start) — show care cho VN community.

**Doc-to-phase mapping** (acceptance của mỗi phase = code + tests + docs cho phần đó):
- Phase 1: `docs/architecture/event-bus.md`, `docs/architecture/bootstrap-pipeline.md`, `docs/services/save.md`, `docs/services/localization.md`, `docs/services/pool.md`, `docs/security/save-encryption.md`, `docs/testing/writing-tests.md`, `docs/testing/ci.md`
- Phase 2: `docs/services/input.md`, `docs/services/audio.md`, `docs/services/notification.md`, `docs/testing/manual-checklist.md`
- Phase 3: `docs/ui/popup-stack.md`, `docs/ui/safe-area.md`, `docs/ui/loading-screen.md`, `docs/ui/toast.md`, `docs/ui/localized-text.md`
- Phase 4 (Gameplay): `docs/gameplay/state-machine.md`, `docs/gameplay/level-loading.md`
- Phase 5 (Live-Ops + DevTools + cross-cutting): `README.md`, `README.vi.md`, `LICENSE`, `CONTRIBUTING.md`, `CHANGELOG.md`, `docs/architecture/asmdef-graph.md` (tổng kết DAG sau khi tất cả tier final), `docs/services/<each-mock>.md` × 8 (extension recipes — Crashlytics, Consent, RemoteConfig, Analytics, Attribution, Ads, IAP, ReceiptValidator), `docs/services/version-check.md`, `docs/services/time.md`, `docs/liveops/version-check.md`, `docs/liveops/addressables-remote.md`, `docs/dev/cheat-console.md`, `docs/dev/fps-overlay.md`, `docs/meta/recipes.md` (per-game patterns, no impl)

---

## 3. Phase Plan

### Phase 1 — Foundation + Remediation (Large, split 1a→1b)

**Goal:** Restructure asmdefs to peer layout, land event bus + localization skeletons, fix save seed footgun, wire bootstrap progress + timeouts + retries, test scaffolding + CI green, ship docs.

**Split into 2 checkpoints** vì asmdef restructure + bootstrap rework có thể compile-break dây chuyền:

**Phase 1a — Foundation compiles green** (no behavior change yet)
- New: `Assets/_Project/Scripts/Runtime/Core/Interfaces/IEventBus.cs`, `IL10nService.cs`, `IBootstrapProgressReporter.cs`
- New: `Assets/_Project/Scripts/Runtime/Services/Events/{R3EventBus.cs, EventsServiceInstaller.cs, Zero.Services.Events.asmdef}`
- New: `Assets/_Project/Scripts/Runtime/Services/Localization/{UnityLocalizationService.cs, MockLocalizationService.cs, LocalizationServiceInstaller.cs, LocalizationStep.cs, Zero.Services.Localization.asmdef}` — wrap `com.unity.localization`, no custom StringTable.
- New: `Assets/_Project/Scripts/Runtime/Infrastructure/BootstrapProgressReporter.cs`
- Edit: `Zero.Gameplay.asmdef`, `Zero.Meta.asmdef` (giữ làm placeholder), `Zero.UI.asmdef` — remove cross-refs giữa 3 layer, add `Zero.Services.Events`
- Edit: `IInputService.cs` — expand interface (impl ở P2; mock vẫn satisfy)
- Edit: `IBootstrapStep.cs` — add `TimeSpan Timeout`, `int MaxRetries` (defaults applied where missing)
- Edit: `BootstrapPipeline.cs` line 31 — per-step progress slicing, inject `IBootstrapProgressReporter`, timeout via linked CTS, retry loop
- Edit: `ProjectScopeInstaller.cs` — register Events + Localization; reorder step list (Save sớm)
- **Pool refactor:** rename `ReflexPoolService.cs` → `UnityPoolService.cs`, swap internal `Stack<GameObject>` → `UnityEngine.Pool.ObjectPool<GameObject>` với `actionOnGet`/`actionOnRelease`/`actionOnDestroy` callbacks. `IPoolService` interface giữ nguyên. Edit `PoolServiceInstaller.cs`.
- **Checkpoint 1a passes when:** Editor compile clean, Bootstrap.unity Press Play runs through all steps with no exceptions, log shows new step order, pool spawn/despawn count match expected.

**Phase 1b — Save hardening + Tests + CI + Docs**
- Edit: `EncryptedJsonSaveService.cs` — read seeds từ `Resources/ZeroSecrets`; throw in player build nếu missing/placeholder
- New: `Assets/Resources/ZeroSecrets.asset.example`, gitignore `ZeroSecrets.asset`
- New: `Assets/_Project/Scripts/Tests/EditMode/{SaveServiceTests.cs (round-trip + tamper + migration), PoolServiceTests.cs (Unity ObjectPool wrap behavior), BootstrapPipelineTests.cs (order, abort, progress, retry, timeout), EventBusTests.cs}`
- New: `.github/workflows/tests.yml`, `.gitignore` updates
- New (docs): `docs/architecture/event-bus.md`, `docs/architecture/bootstrap-pipeline.md`, `docs/services/save.md`, `docs/services/localization.md`, `docs/services/pool.md`, `docs/security/save-encryption.md`, `docs/testing/writing-tests.md`, `docs/testing/ci.md`
- **Checkpoint 1b passes when:** all 4 test suites green locally + CI; docs present matching fixed format.

**Reuse:** `EncryptedJsonSaveService` core (chỉ thay seed source), existing service installer pattern, R3 (already in Core), `com.unity.localization`, `UnityEngine.Pool.ObjectPool`.

**Acceptance:**
- Bootstrap launches OK in Editor; all existing mock services still work.
- Player build with placeholder secrets throws `InvalidOperationException` at startup.
- CI green on push.
- `grep -c "references" Zero.Gameplay.asmdef` shows no `Zero.Meta` or `Zero.UI`.
- 8 doc files exist + match fixed format.
- Pool refactor: same external behavior, internal uses `UnityEngine.Pool.ObjectPool` (verify by reading `UnityPoolService.cs`).

### Phase 2 — Real Input + Audio + Notification (Medium-Large)
**Goal:** Replace 3 mocks (Input, Audio, Notification) bằng real impl wrapping Unity packages. Ship docs + manual checklist.

**Files:**
- New: `Assets/_Project/Scripts/Runtime/Services/Input/UnityInputService.cs` — wrap `com.unity.inputsystem` + EnhancedTouch.
- New: `Assets/_Project/Scripts/Runtime/Services/Audio/AudioMixerService.cs`.
- New: `Assets/_Project/Content/Audio/MainMixer.mixer` (5 buses).
- New: `Assets/_Project/Scripts/Runtime/Services/Notification/UnityMobileNotificationService.cs` — wrap `com.unity.mobile.notifications`, ifdef per-platform.
- Edit: `InputServiceInstaller.cs`, `AudioServiceInstaller.cs`, `NotificationServiceInstaller.cs` — bind real impl. Mock binding kept available qua `#if ZERO_USE_MOCK_<X>` defines cho headless tests.
- Edit: `NotificationStep.cs` — add permission request flow, persist outcome via `ISaveService`.
- New (tests): gesture detection unit tests (tap window, swipe threshold), audio bus volume persistence (SaveService double), notification scheduling round-trip (Mock fallback vì Unity package needs device).
- New (docs): `docs/services/input.md`, `docs/services/audio.md`, `docs/services/notification.md`, `docs/testing/manual-checklist.md`.

**Reuse:** `IPoolService.GetPool<AudioSource>` cho source pool; `ISaveService` cho volume + notification permission persistence.

**Acceptance:**
- EditMode tests green.
- Manual checklist passed: tap/swipe/pinch fire trên device; volume persists across restart; music crossfade smooth (no pop); notification schedule + delivery verified trên iOS + Android device.
- Docs present matching fixed format.

### Phase 3 — UI Scaffolding (Large)
**Goal:** Implement `IUIService` (popup stack + screens + transitions + loading + safe-area + toast + localized text). Ship UI docs.

**Files:**
- New: `Assets/_Project/Scripts/Runtime/UI/{UIService.cs, PopupStack.cs, PopupHandle.cs, ScreenManager.cs, LoadingScreenView.cs, SafeAreaFitter.cs, ToastQueue.cs, LocalizedText.cs, UIServiceInstaller.cs, UIStep.cs}`
- New: `Assets/_Project/Content/UI/Prefabs/` — `LoadingScreen.prefab`, `Toast.prefab`, sample popup prefab, layer canvas prefabs.
- Edit: `Bootstrap.unity` — add 4 layer canvases + LoadingScreen (LoadingScreen subscribes to `IBootstrapProgressReporter`, NOT BootstrapPipeline).
- New (tests): popup stack ordering, modal mask, transition completion, toast FIFO.
- New (docs): `docs/ui/popup-stack.md`, `docs/ui/safe-area.md`, `docs/ui/loading-screen.md`, `docs/ui/toast.md`, `docs/ui/localized-text.md`.

**Reuse:** `IAssetService` cho popup prefab loading; LitMotion cho transitions; `IL10nService` cho text; `IBootstrapProgressReporter` cho loading.

**Acceptance:**
- Push 2 popups → top one modal blocks input → pop → bottom regains focus.
- LoadingScreen shows bootstrap progress 0→1 smooth (verify in manual checklist).
- Safe-area respected trên simulator iPhone notch.
- Toast queue: 3 toasts in row → display sequentially không overlap.
- EditMode tests green; UI docs present.

### Phase 4 — Gameplay Scaffolding (Medium)
**Goal:** State machine + level lifecycle. Genre-specific intentionally absent. Ship gameplay docs.

**Files:**
- New: `Assets/_Project/Scripts/Runtime/Gameplay/{GameStateMachine.cs, IGameState.cs, ILevelDefinition.cs, LevelLoader.cs, GameplayServiceInstaller.cs, Events/{LevelStarted.cs, LevelCompleted.cs, LevelFailed.cs, LevelRestarted.cs, LevelExited.cs}}`
- Sample concrete states: `BootState`, `MenuState`, `PlayState`, `PauseState`, `ResultState` (shells for example).
- New (tests): state transitions, invalid transitions rejected, level lifecycle events, **integration "decoupling test"**.
- New (docs): `docs/gameplay/state-machine.md`, `docs/gameplay/level-loading.md`.

**Reuse:** `IAssetService`, `IEventBus`, `ISceneService`.

**Acceptance:**
- State transitions logged + observable; cannot enter state from invalid predecessor.
- LevelLoader loads ScriptableObject từ Addressables key.
- **Integration "decoupling test":** subscribe UI to `LevelCompleted` (via `IEventBus`) → publish event from Gameplay → popup shown, with no direct ref from `Zero.Gameplay.asmdef` to `Zero.UI.asmdef` (verify asmdef references field).
- Gameplay docs present.

### Phase 5 — Live-Ops + DevTools + Cross-Cutting Docs (Medium)
**Goal:** Version check, cheat console + FPS overlay, repo-level docs + Mock SDK extension recipes + Meta recipes (no impl).

**Files:**
- New: `Assets/_Project/Scripts/Runtime/Services/VersionCheck/{VersionCheckService.cs, VersionCheckServiceInstaller.cs, VersionCheckStep.cs, Zero.Services.VersionCheck.asmdef}`
- New: `Assets/_Project/Scripts/Runtime/DevTools/{CheatConsole.cs, FpsOverlay.cs, ConsoleCommandAttribute.cs, BuiltInCommands.cs, Zero.DevTools.asmdef}` (`#if UNITY_EDITOR || DEVELOPMENT_BUILD`)
- New (tests): version-check decision matrix (ok/soft/force/maintenance), console command parsing.
- New (docs cross-cutting): `README.md`, `README.vi.md`, `LICENSE`, `CONTRIBUTING.md`, `CHANGELOG.md`, `docs/architecture/asmdef-graph.md` (final DAG).
- New (docs services VersionCheck + DevTools): `docs/services/version-check.md`, `docs/services/time.md`, `docs/liveops/version-check.md`, `docs/liveops/addressables-remote.md`, `docs/dev/cheat-console.md`, `docs/dev/fps-overlay.md`.
- New (docs Mock SDK extension recipes): `docs/services/<each-mock>.md` × 8 — Crashlytics, Consent, RemoteConfig, Analytics, Attribution, Ads, IAP, ReceiptValidator. Mỗi file: Mock behavior, real adapter recipe (pseudo-code wrapping Firebase / AppLovin / Unity IAP / etc.), wiring checklist.
- New (docs Meta recipes): `docs/meta/recipes.md` — patterns cho consumer (wallet via `ISaveService`, progression via `ISaveService`+`IEventBus`, A/B variants via `IRemoteConfigService`). Pseudo-code, NO impl ships.

**Reuse:** `IRemoteConfigService` (min_version, maintenance_mode), `IUIService` (maintenance popup), `IL10nService` (maintenance text). Cheat commands tự định nghĩa qua `IConsoleCommand` registry — nếu consumer add Wallet/Progression service, có thể đăng ký commands tương ứng.

**Acceptance:**
- Bump remote `min_version` > local → ForceUpdate gate triggers, popup shown, app blocked.
- Tilde-key toggles console; `save reset` clears + reboots; `loc set <locale>` switches.
- Every public service has `docs/services/<name>.md` matching template.
- README "Quick Start" steps reproducible from clean clone.
- `docs/architecture/asmdef-graph.md` shows final DAG with no cycles.
- `docs/meta/recipes.md` covers: wallet, progression, rewards, variants, daily login (pseudo-code only).

---

## 4. Negative Scope (explicit OUT)

- **Meta layer** — không ship Wallet/Progression/Reward/VariantResolver/DailyLogin. `Zero.Meta.asmdef` giữ làm empty placeholder. Consumer build per-game; recipes trong `docs/meta/recipes.md`.
- Demo game của bất kỳ genre nào.
- Real Firebase/AppsFlyer/Adjust/UnityAds/AppLovin SDK code (Mock + extension docs only). **Exception:** Notification ship real (wraps `com.unity.mobile.notifications`), Localization ship real (wraps `com.unity.localization`).
- CDN/server setup (consumer responsibility).
- Leaderboard, cloud save, multiplayer, social.
- GDPR text drafting (consent flow only, copy blank cho consumer fill).
- Genre-specific gameplay (grid, runner, idle, merge, match-3).
- Multi-platform CI build matrix (chỉ EditMode test trên Linux runner).
- PlayMode tests trong CI (manual checklist thay thế cho thứ Input/Audio/Notification).
- Editor authoring tools beyond minimal SO inspectors (no level editor, no curve editor, **no localization CSV import script** — Unity Localization Package's built-in Editor windows + recipe trong `docs/services/localization.md` đã đủ).
- Custom string-table system (dùng Unity Localization Package thay vì xây).
- Custom object pool (dùng `UnityEngine.Pool.ObjectPool` thay vì `Stack<T>`).
- Asset pipeline (art/audio import settings là consumer's job).
- Unity Catalog / asset store packaging (defer to user khi muốn publish).

---

## 5. Critical Files Map

Files sẽ chạm trong Phase 1 (foundation):
- `/Users/baotran/Desktop/Projects/unity_zero/Assets/_Project/Scripts/Runtime/Bootstrap/BootstrapPipeline.cs`
- `/Users/baotran/Desktop/Projects/unity_zero/Assets/_Project/Scripts/Runtime/Bootstrap/ProjectScopeInstaller.cs`
- `/Users/baotran/Desktop/Projects/unity_zero/Assets/_Project/Scripts/Runtime/Services/Save/EncryptedJsonSaveService.cs`
- `/Users/baotran/Desktop/Projects/unity_zero/Assets/_Project/Scripts/Runtime/Services/Pool/ReflexPoolService.cs` → rename `UnityPoolService.cs`
- `/Users/baotran/Desktop/Projects/unity_zero/Assets/_Project/Scripts/Runtime/Core/Interfaces/IInputService.cs`
- `/Users/baotran/Desktop/Projects/unity_zero/Assets/_Project/Scripts/Runtime/Core/IBootstrapStep.cs`
- `/Users/baotran/Desktop/Projects/unity_zero/Assets/_Project/Scripts/Runtime/Gameplay/Zero.Gameplay.asmdef`
- `/Users/baotran/Desktop/Projects/unity_zero/Assets/_Project/Scripts/Runtime/Meta/Zero.Meta.asmdef`
- `/Users/baotran/Desktop/Projects/unity_zero/Assets/_Project/Scripts/Runtime/UI/Zero.UI.asmdef`

Files sẽ tạo mới (toàn bộ phases):
- ~25 .cs runtime + ~10 .cs test + ~22 .md doc + 1 mixer asset + ~5 prefabs + 1 CI yaml + 1 ScriptableObject template (`ZeroSecrets.asset.example`).

---

## 6. Verification Plan

**Per phase:**
- Editor verification: open Bootstrap.unity, Press Play, scan `[Bootstrap] Step N/M:` log → expected step order + no exceptions.
- EditMode tests pass via `Window → Test Runner` hoặc headless: `Unity -batchmode -nographics -projectPath . -runTests -testPlatform editmode -testResults results.xml -quit`.
- Verify asmdef DAG acyclic: `find Assets/_Project -name "*.asmdef" | xargs cat | grep '"references"'` + manual review.

**End-to-end (after Phase 4):** "decoupling" integration test publishes `LevelCompleted` via `IEventBus`, asserts UI subscriber receives it + popup shown, **without** any direct `using Zero.UI` trong Gameplay asmdef. Verify `grep "Zero.UI" Zero.Gameplay.asmdef` returns nothing.

**Final (after Phase 6):**
- Clean clone → follow README Quick Start → runs.
- CI green.
- All docs/*.md exist, structure matches template.
- Search repo cho `TODO`, `FIXME`, `XXX` → none of category "block-ship".

---

## 7. Defaults Applied (flag nếu disagree, không cần answer từng câu)

Em sẽ áp dụng các default sau khi implement. Nếu anh muốn đổi cái nào, ghi rõ khi reject ExitPlanMode.

| # | Default | Lý do |
|---|---------|-------|
| 1 | **License: MIT** | Phổ biến nhất cho game template, permissive. |
| 2 | **Unity version frozen ở `6000.3.11f1`**, document upgrade matrix sau | Tránh CI break khi LTS bump. |
| 3 | **Save model: single slot v1**; multi-profile defer v2 | Hybrid casual đa số single-slot. |
| 4 | **State machine: flat** | Đa số genre dùng flat đủ; HSM over-engineer. |
| 5 | **Crypto seed: ScriptableObject (gitignored) + `.example`** | Unity-native, đơn giản; env-var-at-build alt document riêng. |
| 6 | **Mock telemetry: log event names** | Helpful for dev QA, cleaner than no-op. |
| 7 | **Asmdef count sau v2: ~25** | Acceptable; consolidation defer. |
| 8 | **Locale key style: namespaced** (`ui.popup.win.title`) | Tránh collision khi project lớn. |
| 9 | **Doc language: English primary + `README.vi.md` (pitch + Quick Start)** | Front door cho international + show care cho VN community. |
| 10 | **PlayMode CI: skip; manual checklist thay thế cho Phase 2 features** | Đơn giản hơn; CI EditMode-only đủ cho v1. |

---

## 7b. Architecture Decisions (Codex review-grade defense notes)

Những chỗ Codex sẽ pick on, em đã defend trong code/docs:

- **Asmdef inflation justification (4 new asmdefs):** mỗi asmdef mới phải defend trong commit message + `docs/architecture/asmdef-graph.md`:
  - `Zero.Services.Events`: separate vì interface trong Core nhưng impl phụ thuộc R3 → cần asmdef để cô lập impl khỏi consumers chỉ cần interface.
  - `Zero.Services.Localization`: separate vì có ScriptableObject + Addressables dependency; consumer có thể strip nếu single-language game.
  - `Zero.Services.VersionCheck`: separate vì optional (Editor-only games không cần); consumer có thể strip.
  - `Zero.DevTools`: separate + ifdef-gated; ship trong same package nhưng không trong production builds.
  Total ~25 asmdefs — Codex sẽ comment, em sẽ point tới này.

- **`R3EventBus` storage type:** `Dictionary<Type, object>` storing `Subject<T>`, cast on access. **Document trong `docs/architecture/event-bus.md`:** value-type events get boxed via Subject<T> internal queue — acceptable cho hybrid casual event rate (<1000/s); nếu high-frequency cần struct events, consumer dùng R3 stream riêng outside bus.

- **`ProjectScopeInstaller internal static`:** đổi sang `public static partial class` cho consumer extend qua `partial` extension files (`ProjectScopeInstaller.UserServices.cs`). Document pattern trong `CONTRIBUTING.md` + Quick Start.

- **`IBootstrapProgressReporter` lifetime:** Singleton, registered trong `Zero.Infrastructure`. Pipeline injects + writes only; LoadingScreen + any HUD progress display injects + reads only. Tránh exposing Subject trực tiếp (clean separation read/write).

- **Save reset-to-empty on decrypt fail:** giữ nguyên behavior (line 72, 86), document `IFailLoudHandler` extension point cho consumer override. Defendable trong `docs/security/save-encryption.md`: hybrid casual single-slot → recoverable progress > hard fail; nếu economy quan trọng → server validation.

---

## 8. Effort Rough Estimate

| Phase | Effort | Lý do |
|-------|--------|-------|
| 1 (split 1a/1b) | Large | Asmdef restructure + bootstrap rework + Pool refactor + 4 test suites + CI |
| 2 | Medium-Large | 3 real impls (Input + Audio + Notification); Input gesture detection nontrivial; iOS/Android ifdef cho Notification |
| 3 | Large | Popup stack + transitions + 5+ prefabs + 4 layer canvases |
| 4 | Medium | State machine + level lifecycle, ít UI |
| 5 | Medium | DevTools + ~22 docs files; nhiều file nhưng pattern lặp |

5 phases total. Tổng: framework hoàn chỉnh ở mức tối giản (no Meta) + opensource-ready.

---

## 9. Implementation Workflow & Context Strategy

Mục tiêu: main session context ≤50% (target ~10-15%) suốt 5 phases để Claude luôn ở trạng thái thông minh nhất; survive session clears không mất context.

### 9.1 Repo setup (one-time, before Phase 1)
- Repo remote: `https://github.com/tnbao91/unity_zero.git`
- Initial commit: existing codebase as-is (snapshot trước khi v2 work bắt đầu) → tag `pre-v2`.
- Move plan: `~/.claude/plans/t-i-ang-mu-n-build-snuggly-moonbeam.md` → `docs/dev/PLAN.md` trong repo. Plan thành public artifact của template, contributors + Codex đọc được.
- Create `docs/dev/JOURNAL.md` với header (empty journal, append-only).
- Create `.gitignore` đầy đủ Unity (Library/, Temp/, Logs/, Build/, .vs/, *.csproj, *.slnx, ZeroSecrets.asset).
- Push lên `main`.

### 9.2 Per-phase loop (em sẽ làm)

Mỗi phase:
1. Tạo branch `phase-<N>-<short-name>` (vd `phase-1a-foundation`).
2. Spawn subagent với `isolation: "worktree"` — agent đọc `docs/dev/PLAN.md` + tail `docs/dev/JOURNAL.md` để có context, implement phase đó, run EditMode tests, commit incremental, return summary ≤300 words.
3. Em (main session) review summary, nếu OK → merge worktree branch vào `phase-<N>` branch.
4. **Append journal entry** vào `docs/dev/JOURNAL.md`:
   ```
   ## Phase <N> — YYYY-MM-DD (commit <sha>)
   - Files touched: <list>
   - Key decisions: <deviations from plan if any>
   - Tests: <X/Y passing>
   - Resume hint: <next phase prerequisite, known gotchas>
   ```
5. Push branch + journal.
6. Open PR (cho Codex review) → wait for user merge → next phase.

### 9.3 Session resume protocol (anh quay lại sau session clear)

Anh gõ "tiếp tục Phase X" hoặc "tiếp tục":
1. Em đọc `docs/dev/PLAN.md` (1 lần, ~3K tokens).
2. Em đọc tail 80 dòng `docs/dev/JOURNAL.md` (~500 tokens).
3. Em xác định phase tiếp theo từ journal cuối, confirm với anh, rồi spawn subagent.

Anh không cần nhớ bất cứ thứ gì — plan + journal là source-of-truth.

### 9.4 Token budget per phase (target)
| Operation | Tokens (main) |
|---|---|
| Read PLAN.md đầu phiên | ~3,000 (one-time per session) |
| Read JOURNAL.md tail | ~500 |
| Subagent prompt + summary | ~2,000/phase |
| Review + journal append | ~1,500/phase |
| **Per-phase main cost** | **~4,000-5,000** |
| **5 phases trong 1 session** | **~25,000 (~12% of 200K)** |

Anh có thể gõ `/compact` bất cứ lúc nào để trim thêm.

### 9.5 Memory hygiene
- Cross-session decisions đã save: `feedback_unity_defaults.md`, `project_unity_zero_scope.md`.
- Em sẽ save thêm khi gặp decision mới quan trọng (vd: nếu phát sinh deviation trong Phase 1, save reason).
- KHÔNG save: file paths, commit SHAs, ephemeral progress (đã có journal).

### 9.6 Rollback strategy
- Mỗi phase = 1 branch + 1 commit topology rõ ràng. Nếu sai: `git revert <sha>` hoặc reset branch.
- Worktree isolation = sai trong subagent không ảnh hưởng main repo.
- Tag `pre-v2` đầu cuộc làm template = ultimate rollback point.
