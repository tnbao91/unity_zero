# Available services (cheatsheet)

Every interface lives in `Zero.Core` namespace. Implementations bound at root scope; resolve via Reflex `[Inject]`.

> Detailed per-service docs at upstream <https://github.com/tnbao91/unity_zero/tree/main/docs/services>.

## Core infra

| Interface | Notes |
|---|---|
| `ILogService` | Wrap `UnityEngine.Debug` with level + tag. `Debug/Info/Warn/Error/Critical`. `IsEnabled` toggle. |
| `IDeviceProfileService` | Tier (`Low/Mid/High`), platform info, screen DPI. Used by tier-aware code. |
| `ISaveService` | `Set<T>(key, value)`, `Get<T>(key, default)`, `Has(key)`, `Delete(key)`, `SaveAsync()`, `LoadAsync()`. AES-CBC + HMAC. JSON envelope `{ "version": 1, "data": {...} }`. Migration via `Migrate(JObject, fromVer, toVer)`. |
| `IAssetService` | Addressables wrapper. `LoadAsync<T>(key, ct)`, `LoadHandleAsync<T>(key, ct)`, `HasKeyAsync<T>(key, ct)`, `Release(key)`. **Always use `HasKeyAsync` before `LoadAsync` for optional keys** — Addressables logs red errors on missing keys before throwing. |
| `ISceneService` | Addressables-backed scene load/unload. `LoadAsync(key, mode, ct)`, `UnloadAsync(scene, ct)`. |
| `IPoolService` | `GetPool<T>(prefab|key, prewarm)`, `Spawn`, `Despawn`. Wraps `UnityEngine.Pool.ObjectPool<GameObject>` + Addressables. |
| `IEventBus` | `Observable<T> On<T>()`, `Publish<T>(T evt)`. Lazy `Dictionary<Type, Subject<T>>` storage. R3-backed. |
| `IBootstrapProgressReporter` | `Observable<float> Progress`, `Observable<string> CurrentStepName`. Read-only for views; pipeline writes. |

## Cross-cutting platform

| Interface | Notes |
|---|---|
| `IL10nService` | `Get(key, args)`, `Observable<Locale> OnLocaleChanged`, `SetLocaleAsync(localeId, ct)`, `CurrentLocale`. Wraps `com.unity.localization`. Mock available via `MockLocalizationService`. |
| `IAudioService` | Bus volumes (Master/Music/Sfx/Ui/Voice) persisted via `ISaveService`. Music crossfade via LitMotion. SFX one-shot via pooled AudioSource. Falls back to per-source volume if no mixer asset at Addressables key `audio/main_mixer`. |
| `IInputService` | New Input System + EnhancedTouch. Observables: `OnPointerDown/Up/Tap/Drag`, `OnSwipe`, `OnPinch`, `OnEscape`. Gestures: tap <200ms+<20px, swipe ≥50px in <500ms. |
| `INotificationService` | Wraps `com.unity.mobile.notifications` Unified API. Schedule, cancel, permission. Permission **NOT** requested at bootstrap — call `RequestPermissionAsync` at a "value moment". |
| `ITimeService` | Currently a stub returning `DateTime.UtcNow`. Replace per-game with NTP/server time when economy needs it. |

## Live-Ops + monetization

| Interface | Notes |
|---|---|
| `IConsentService` | GDPR/ATT consent. Mock returns "all granted." Consumer fills with real Google UMP / Unity ATT. |
| `IRemoteConfigService` | `GetString/Bool/Int/Float`. Mock returns hardcoded defaults. Real adapter for Firebase Remote Config / Unity Remote Config. |
| `IAnalyticsService` | `LogEvent(name, params)`. Mock logs to Console. Real: GA4 / Firebase Analytics / Unity Analytics. |
| `IAttributionService` | Install attribution. Real: AppsFlyer / Adjust. |
| `ICrashlyticsService` | `LogException(ex)`, `SetUserId(id)`, `SetCustomKey(key, value)`. Real: Firebase Crashlytics / Sentry. |
| `IAdsService` | Banner/Interstitial/Rewarded. Real: AppLovin MAX / IronSource / AdMob. |
| `IAdPlacementService` | Caps, cooldowns, frequency rules. Production-ready (`DefaultAdPlacementService`), genre-tunable. |
| `IIapService` | Purchase, restore, products. Real: Unity IAP / RevenueCat. |
| `IReceiptValidator` | Server-side or client-side validation. Mock = `StubReceiptValidator` (always-valid). |
| `IVersionCheckService` | Compare `Application.version` vs remote `min_version` / `recommended_version` / `maintenance_mode`. `LastResult.Status: Ok | SoftUpdate | ForceUpdate | Maintenance`. Consumer reads + decides UI. |

## UI

| Interface | Notes |
|---|---|
| `IUIService` | `PushAsync<TPopup, TData, TResult>(data, ct)`, `PopAsync(handle)`, `ShowScreen<TScreen>`, `Toast(text, duration)`. Layer-aware (Hud/Popup/Overlay/System). **Throws if no `UIRoot` attached** — consumer adds `UIRoot` MonoBehaviour to scene with 4 Transform slots. |
| `IBootstrapProgressReporter` | (Listed under Core; UI consumes for loading screens.) |

## Gameplay

| Interface | Notes |
|---|---|
| `IGameStateMachine` | `CurrentState`, `Observable<IGameState> OnStateChanged`, `ChangeStateAsync(IGameState, ct)`. Flat states. **Concurrent calls throw** — await previous before next. **Same-instance re-entry throws** — create fresh state instance. Consumer drives `Tick(deltaTime)` from update loop. |
| `IGameState` | `EnterAsync(ct)`, `ExitAsync(ct)`, `Tick(deltaTime)`. The template ships no example states — you author `IGameState` impls per genre; pattern in upstream `docs/gameplay/state-machine.md`. |
| `LevelLoader` | Self-bound helper. `LoadLevelAsync(addressableKey, ct)` returns `(GameObject Instance, IAssetHandle<GameObject> Handle)`. Caller owns disposal. |
| `ILevelDefinition` | Abstract `ScriptableObject` (`Id`, `DisplayName`, `AddressablePrefabKey`). Subclass per genre. |

## Domain events on `IEventBus`

Lifecycle (5 events from `Zero.Gameplay.Events`):
- `LevelStarted(level, mode)`
- `LevelCompleted(level, score, stars, durationSec)`
- `LevelFailed(level, reason)`
- `LevelRestarted(level)`
- `LevelExited(level, reason)`

Cross-cutting from `Zero.Core`:
- `AppPaused`, `AppQuitting`

Add YOUR game events as `readonly struct` POCOs in YOUR asmdef. Don't put them in `Zero.Core`.
