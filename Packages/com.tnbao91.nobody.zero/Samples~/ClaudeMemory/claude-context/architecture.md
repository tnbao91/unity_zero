# Architecture (consumer-side cheatsheet)

This is what you GET when you install Zero. Not what you build — that's your game.

## Asmdef tier diagram

```
┌─────────────────────────────────────────┐
│ Zero.Core           interfaces, POCOs   │
│   ↑                                     │
│ Zero.Infrastructure BootstrapStepBase   │
│   ↑                                     │
│ Zero.Services.<24>  one asmdef per svc  │
│   ↑       ↑       ↑                     │
│ Zero.UI Zero.Meta Zero.Gameplay         │ ← peers, never cross-ref
│       ↘    ↓    ↙                       │
│ Zero.Bootstrap      composition root    │
│       ↑                                 │
│ Game.<YourGame>     YOUR asmdef         │
└─────────────────────────────────────────┘
```

`autoReferenced: false` on every Zero asmdef. To use a Zero service, your asmdef must explicitly list it under `references` in your `.asmdef` file.

## Peer rule (important)

Gameplay, UI, and Meta are **peers**. No direct asmdef references between them. Cross-tier coupling goes through `IEventBus`:

```csharp
// In Gameplay
_bus.Publish(new LevelCompleted(level: 5, score: 1234));

// In UI (separate asmdef, no Zero.Gameplay reference needed at compile time;
// only the event POCO's asmdef must be referenced)
_bus.On<LevelCompleted>().Subscribe(evt => ShowResultPopup(evt));
```

Apply the same rule in YOUR game when subsystems should be loosely coupled. Only use direct calls when subsystems are tightly bound by definition.

## Bootstrap pipeline

`BootstrapPipeline` runs 16 sequential `IBootstrapStep` items at app start:

```
1.  Crashlytics       (critical — aborts on fail)
2.  Log
3.  DeviceProfile
4.  Save              (early so later steps can read settings)
5.  Asset             (Addressables init)
6.  Consent
7.  RemoteConfig
8.  Analytics
9.  Localization      (warns if no LocalizationSettings)
10. Attribution
11. Ads
12. IAP
13. Audio             (degrades gracefully if no mixer asset)
14. Time
15. Notification      (initialize only — permission requested at "value moment")
16. VersionCheck      (consumer reads LastResult to decide UI gate)
```

Each step:
- Has a **timeout** (default 30s, override per step).
- Retries on non-critical failure (default 1 retry).
- Aborts pipeline only if `IsCritical = true` (Crashlytics only).
- Reports progress to `IBootstrapProgressReporter` (Singleton).

Consumer reads progress via `IBootstrapProgressReporter.Progress` Observable for loading screens.

## DI flow

1. `ProjectScopeInstaller.Hook()` registered with `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` — fires before any scene loads.
2. On `ContainerScope.OnRootContainerBuilding`, every `<Service>ServiceInstaller.Install(builder)` runs, binding services to the root container.
3. `BootstrapPipeline` is bound `Lifetime.Singleton, Resolution.Lazy` so the explicit step list defines bootstrap order (not DI rediscovery).
4. In `Bootstrap.unity`, `GameLauncher` MonoBehaviour with `[DefaultExecutionOrder(-100)]` gets `[Inject]`-ed and runs the pipeline in `Start()`.
5. After bootstrap, your game scenes load (Addressables-backed via `ISceneService`).

To resolve a service from a non-injected MonoBehaviour: `var svc = Container.RootContainer.Resolve<IFooService>();` (NOT `ContainerScope.Root` — that doesn't exist).

To construct a class that ISN'T registered as a contract but whose ctor takes registered services: `var cmd = container.Construct(typeof(MyCommand));` — NOT `Resolve(Type)` which throws `UnknownContractException`.

## Mock-first defaults

Most third-party SDK services ship with `Mock<Name>Service` implementations. The mocks log event names so dev/QA can see what's happening without real SDKs. Replace per-game by swapping the binding in the service's installer:

```csharp
// In YOUR game's installer, after Zero's installers run
public static class MyGameOverridesInstaller
{
    public static void Install(ContainerBuilder builder)
    {
        builder.RegisterType<FirebaseCrashlyticsService>(typeof(ICrashlyticsService),
            Lifetime.Singleton, Resolution.Lazy);
        // overrides the previous Mock binding
    }
}
```

Real impls already shipping (NOT mocks):
- `UnityLocalizationService` (wraps `com.unity.localization`)
- `R3EventBus` (impl of `IEventBus`)
- `UnityPoolService` (wraps `UnityEngine.Pool.ObjectPool`)
- `UnityInputService` (wraps Input System + EnhancedTouch)
- `AudioMixerService` (wraps Unity AudioMixer)
- `UnityMobileNotificationService` (wraps `com.unity.mobile.notifications`)
- `UIService` (Zero's own popup/screen/toast manager)
- `GameStateMachine` (Zero's own flat-state impl)
- `VersionCheckService` (Zero's own remote-config-driven gate)

Full list with extension recipes at upstream `docs/services/`.
