# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Unity 6 LTS (`6000.3.11f1`) greenfield template for hybrid casual games. Single bootstrap scene at `Assets/_Project/Scenes/Bootstrap.unity`. URP, Input System (new), Addressables, IAP, NuGetForUnity.

## Stack (deliberate choices — do not substitute)

- **DI**: Reflex (not Zenject/VContainer)
- **Async**: UniTask (not Task<T> for game code)
- **Reactive**: R3 (not UniRx)
- **Tweening**: LitMotion (not DOTween)
- **JSON**: Newtonsoft.Json via NuGetForUnity
- **String building**: ZString

Packages come from OpenUPM (Cysharp/AnnulusGames/Reflex) plus Unity registry. NuGet packages are listed in `Assets/packages.config` and managed by NuGetForUnity.

## Architecture

The codebase is split into ~24 assembly definitions enforcing a strict dependency direction. **Gameplay/Meta/UI are peers** — they never reference each other; they cross-talk only through `IEventBus` (Phase 1a, see `docs/dev/PLAN.md` §2.1).

```
Zero.Core (interfaces, POCOs, cross-cutting events; references UniTask + R3)
   ↑
Zero.Infrastructure (BootstrapStepBase + BootstrapProgressReporter)
   ↑
Zero.Services.<Name>  (one asmdef per service; includes Zero.Services.Events, Zero.Services.Localization)
   ↑          ↑          ↑
Zero.UI   Zero.Meta   Zero.Gameplay   ← peers, talk via IEventBus
        ↘     ↓     ↙
      Zero.Bootstrap (composition root, references every service + every peer)
```

`Zero.Core` only holds interfaces (`I*Service`) and POCOs. Services never reference each other directly — they cross-talk through interfaces resolved from the Reflex container. Cross-asmdef domain events go through `IEventBus` (impl `R3EventBus` in `Zero.Services.Events`); typed `Subject<T>` per event type, no direct subscriber/publisher coupling.

### DI bootstrap flow

1. `ProjectScopeInstaller.Hook()` is registered with `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` and subscribes to `ContainerScope.OnRootContainerBuilding`.
2. On container build, it calls each `<Service>ServiceInstaller.Install(builder)` to register that service's binding.
3. It registers a `BootstrapPipeline` factory with `Lifetime.Singleton, Resolution.Lazy` — the **explicit step list** in `ProjectScopeInstaller.InstallBindings` defines bootstrap order. Reorder there, not by DI rediscovery.
4. In the Bootstrap scene, a `GameLauncher` MonoBehaviour (`[DefaultExecutionOrder(-100)]`) gets `[Inject]`-ed and runs the pipeline in `Start()`.

### Bootstrap steps

Each step extends `BootstrapStepBase` (in `Zero.Infrastructure`) and declares `Name`, `IsCritical`, and optionally overrides `Timeout` (default 30s) and `MaxRetries` (default 1). Pipeline behavior:

- Steps run **sequentially** in the order defined in `ProjectScopeInstaller`.
- Each step runs inside a linked CTS that fires `CancelAfter(step.Timeout)`.
- Non-critical steps that throw (other than `OperationCanceledException` from the outer token) are retried up to `MaxRetries` times, then logged + swallowed.
- If `IsCritical = true` and the step throws after retries, the pipeline aborts. Currently only `CrashlyticsStep` is critical.
- `OperationCanceledException` from the outer (caller) token always propagates; timeout cancellation is treated as a step failure subject to retry policy.
- Per-step progress is sliced into the overall pipeline progress and written to `IBootstrapProgressReporter` (Singleton in `Zero.Infrastructure`). Pipeline writes; `LoadingScreenView` and any HUD progress UI read. Never resolve `BootstrapPipeline` directly from a view — read from the reporter to avoid the Lazy-singleton resolution race.

### Service convention

Every service follows the same shape — match it when adding new ones:

1. Interface in `Assets/_Project/Scripts/Runtime/Core/Interfaces/I<Name>Service.cs` (namespace `Zero.Core`).
2. Implementation in `Assets/_Project/Scripts/Runtime/Services/<Name>/` with its own `Zero.Services.<Name>.asmdef`.
3. `<Name>ServiceInstaller` static class with `Install(ContainerBuilder builder)` — almost always a single `RegisterType` call with `Lifetime.Singleton, Resolution.Lazy`.
4. If it needs initialization at startup, a `<Name>Step : BootstrapStepBase` in `Assets/_Project/Scripts/Runtime/Bootstrap/Steps/`.
5. Wire it in `ProjectScopeInstaller.InstallBindings`: add the `Install(builder)` call, then add the step to the `steps` array in the correct position.

### Mock-first template defaults

Most third-party SDK integrations (Ads, IAP, Analytics, Crashlytics, RemoteConfig, Notification, Attribution, Audio, Consent, Input) ship with `Mock<Name>Service` implementations. These are placeholders so the template runs end-to-end without real SDKs — replace with real adapters per game by swapping the binding in the service's installer.

**Exceptions (real impls already shipping):**
- `UnityLocalizationService` (`Zero.Services.Localization`) wraps `com.unity.localization`. `LocalizationStep` short-circuits with a warning when no `LocalizationSettings` asset is configured, so the template still launches on a fresh clone. A `MockLocalizationService` is also available for headless tests.
- `R3EventBus` (`Zero.Services.Events`) is the only impl of `IEventBus`; it is not "mock vs real" — it's the single production impl.
- `UnityPoolService` (`Zero.Services.Pool`) wraps `UnityEngine.Pool.ObjectPool<GameObject>` (renamed from `ReflexPoolService` in Phase 1a; the old name is misleading — nothing in it uses Reflex's pool framework, it's just a Reflex-bound service).

## Things that are easy to miss

- **Save encryption seeds are placeholders.** `EncryptedJsonSaveService.DefaultAesSeed` / `DefaultHmacSeed` must be replaced with per-game secrets before shipping. The file format is `[HMAC-SHA256 32B][IV 16B][AES-CBC ciphertext]` wrapping a versioned JSON envelope `{ "version": 1, "data": {...} }`. Migrations live in `Migrate(JObject, from, to)`.
- **Reflex root scopes list is empty** in `Assets/Resources/ReflexSettings.asset` — that's intentional. The root container is built via `OnRootContainerBuilding` from `ProjectScopeInstaller`, not from a scope ScriptableObject.
- **Only `Bootstrap.unity` is in build settings.** Game scenes are loaded via the scene service (Addressables-backed).
- **`*.csproj` and `unity_zero.slnx` are gitignored** — they're regenerated by Unity. Don't edit them by hand.
- **Each asmdef has `autoReferenced: false`.** Adding a reference between assemblies requires editing both the consumer's `.asmdef` and (if you introduce a new service) `Zero.Bootstrap.asmdef`. This also bites when wrapping Unity packages whose public API surfaces transitive types — e.g. `Zero.Services.Localization` must list `Unity.ResourceManager` (because `LocalizationSettings.InitializationOperation` returns `AsyncOperationHandle<>`) and `UniTask.Addressables` (for the `.ToUniTask()` extension on that handle).
- **Gameplay/Meta/UI must not reference each other.** They are peers — Codex review will flag any direct ref. Cross-tier coupling goes through `IEventBus`. Verify with `grep "Zero.UI\|Zero.Meta" Assets/_Project/Scripts/Runtime/Gameplay/Zero.Gameplay.asmdef` (must return empty).
- **Plan + journal are source of truth for in-flight work.** `docs/dev/PLAN.md` defines phases + decisions; `docs/dev/JOURNAL.md` is append-only history of what landed. After every phase, update the journal AND this CLAUDE.md before merging — see `feedback_phase_workflow.md` in user memory.

## Build & test

This is a Unity project — there is no shell-level build script. Operate via the Editor:

- **Open**: open the project in Unity 6.0.3.11f1 (matching `ProjectSettings/ProjectVersion.txt`).
- **Play**: open `Assets/_Project/Scenes/Bootstrap.unity` and press Play. The bootstrap pipeline log lines (`[Bootstrap] Step N/M: ...`) appear in the Console.
- **Tests**: `Window → General → Test Runner`. EditMode and PlayMode test asmdefs exist at `Assets/_Project/Scripts/Tests/{EditMode,PlayMode}/` but are currently empty. Both are gated on the `UNITY_INCLUDE_TESTS` define and reference NUnit + Unity's TestRunner.
- **Headless test run** (when needed):
  ```
  Unity -batchmode -nographics -projectPath . -runTests -testPlatform editmode -testResults results.xml -quit
  ```

When verifying changes, prefer letting the user run the Editor — Claude can't open Unity. State explicitly when a change needs Editor verification rather than claiming it works.
