# Bootstrap Pipeline

## Overview

The bootstrap pipeline is a **two-phase, resilient startup sequence** that initializes all services from a single, reorderable list. **Blocking** steps run first and are all the player waits for; `BootstrapReady` then publishes and the **deferred** steps continue in the background while the player is already in the game. Only four of the sixteen shipped steps are blocking. Each step (Crashlytics, Save, Assets, Localization, Ads, etc.) is optional, has configurable timeout/retry/criticality, and reports progress to a `IBootstrapProgressReporter` for UI display. No shipped step is critical: a step that fails is recorded in `IBootstrapReport`, announced as `BootstrapStepDegraded`, and the pipeline runs on so the player still reaches the game. A *consumer* step that opts into `IsCritical` aborts the entire pipeline — the abort publishes `BootstrapFailed` on `IEventBus` and surfaces as `BootstrapStepFailedException`, and a consumer can publish `BootstrapRetryRequested` to make `GameLauncher` re-run the pipeline (see "Failure & retry" below).

## How the root container is built

The Reflex root scopes list in `Assets/Resources/ReflexSettings.asset` is **intentionally empty**. The root container is built imperatively from `ProjectScopeInstaller.Hook()`, which is registered with `[RuntimeInitializeOnLoadMethod(BeforeSplashScreen)]` and subscribes to `ContainerScope.OnRootContainerBuilding`. On the build event, `InstallBindings` calls each `<Service>ServiceInstaller.Install(builder)` and registers a `BootstrapPipeline` factory with `Lifetime.Singleton, Resolution.Lazy`. The Bootstrap scene's `GameLauncher` MonoBehaviour (`[DefaultExecutionOrder(-100)]`) gets `[Inject]`-ed and runs the pipeline in `Start()`.

The load-type choice is deliberate ordering, not style: Reflex resets `OnRootContainerBuilding` at `AfterAssembliesLoaded`, the package subscribes at `BeforeSplashScreen`, and consumer installers subscribe at `BeforeSceneLoad`. Template bindings therefore always register **first**, so a consumer's re-registration of the same contract deterministically wins (Reflex resolves the **last** binding per contract). Cross-assembly ordering inside the same load type is unspecified in Unity — that is why the package does not sit at `BeforeSceneLoad` with the consumers. The actual container build happens lazily when the first scene containing a `ContainerScope` loads, which is after all of the above.

Do not add scopes to the `ReflexSettings.asset` list expecting it to fire — the wiring is in code, not data.

## Public API

```csharp
// In Zero.Bootstrap
public sealed class BootstrapPipeline
{
    public BootstrapPipeline(
        IReadOnlyList<IBootstrapStep> steps,
        ILogService log,
        IBootstrapProgressReporter reporter,
        IEventBus eventBus = null,    // publishes BootstrapFailed / BootstrapStepDegraded
        IBootstrapReport report = null); // durable record of degraded steps

    public UniTask RunAsync(IProgress<float> overallProgress, CancellationToken ct);
}

// In Zero.Core — thrown by RunAsync when a critical step fails or times out
public sealed class BootstrapStepFailedException : Exception
{
    public string StepName { get; }
    public int Attempt { get; }
    // InnerException = the original failure (or TimeoutException)
}

// In Zero.Core.Events — published on IEventBus
public readonly struct BootstrapFailed { string StepName; Exception Error; int Attempt; }
public readonly struct BootstrapStepDegraded { string StepName; Exception Error; int Attempts; }
public readonly struct BootstrapRetryRequested { } // publish to re-run the pipeline

// In Zero.Core — consumer seam to extend the step list (see Extension Points)
public enum BootstrapStepAnchor { Append, Before, After, Replace }
public sealed class BootstrapStepRegistration
{
    public BootstrapStepRegistration(IBootstrapStep step,
        BootstrapStepAnchor anchor = BootstrapStepAnchor.Append,
        string anchorStepName = null);
}

// Base class in Zero.Infrastructure for all steps
public abstract class BootstrapStepBase : IBootstrapStep
{
    public abstract string Name { get; }
    public virtual bool IsCritical => false;
    public virtual TimeSpan Timeout => TimeSpan.FromSeconds(30);
    public virtual int MaxRetries => 1;

    protected abstract UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct);
}

// Reporting interface in Zero.Core
public interface IBootstrapProgressReporter
{
    Observable<float> Progress { get; }           // 0.0 to 1.0
    Observable<string> CurrentStepName { get; }   // "Loading Assets" etc.
    void Report(float progress, string stepName); // pipeline writes; implement it in a custom reporter
}

// Durable "did anything fail?" record in Zero.Core — read this when you are asking
// after the fact, since the bus does not replay BootstrapStepDegraded to late subscribers.
public interface IBootstrapReport
{
    bool IsHealthy { get; }
    IReadOnlyList<DegradedStep> Degraded { get; }
    bool IsDegraded(string stepName);             // ordinal match on IBootstrapStep.Name
    void Record(string stepName, Exception error, int attempts); // pipeline writes
    void Clear();                                 // pipeline clears at the start of each run
}
```

## Extension Points

**Custom bootstrap steps (consumer-side, no package edit):** inherit `BootstrapStepBase`, override `OnExecuteAsync`, then register a `BootstrapStepRegistration` from your own installer. The pipeline factory composes registrations onto the default list in registration order; `Before`/`After`/`Replace` anchor on an `IBootstrapStep.Name` already in the list, and a typo'd anchor name throws at boot instead of silently skipping your step.

```csharp
public sealed class MyCustomStep : BootstrapStepBase
{
    public override string Name => "MyCustom";
    public override bool IsCritical => false;      // Non-critical failures don't block
    public override TimeSpan Timeout => TimeSpan.FromSeconds(10);
    public override int MaxRetries => 3;

    protected override async UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
    {
        // Init your service here. Throw if failed.
        // Pipeline will retry up to MaxRetries times (non-critical),
        // or abort (critical).
        await _myService.InitAsync(ct);
    }
}

// Assets/_Game/Bootstrap/MyGameInstaller.cs — YOUR asmdef
public static class MyGameInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Hook() => ContainerScope.OnRootContainerBuilding += Install;

    private static void Install(ContainerBuilder builder)
    {
        builder.RegisterFactory(
            c => new BootstrapStepRegistration(
                new MyCustomStep(c.Resolve<IAssetService>()),
                BootstrapStepAnchor.After, "Save"),
            new[] { typeof(BootstrapStepRegistration) },
            Lifetime.Singleton, Resolution.Lazy);
    }
}
```

`BootstrapStepAnchor.Replace` also covers "swap a mock step's service for a real SDK step" when re-binding the service interface alone isn't enough (e.g. the real SDK needs a different init shape).

**Maintainer-side (this repo):** template-default steps stay hard-coded in the `steps[]` array inside `ProjectScopeInstaller.InstallBindings()` — explicit order, reviewable in one place.

**Failure & retry:** on a critical abort the pipeline publishes `BootstrapFailed` and `GameLauncher` logs the wrapped `BootstrapStepFailedException`. A consumer loading screen owns the retry UX:

```csharp
[Inject] private IEventBus _bus;

private void OnEnable()
{
    _failedSub = _bus.On<BootstrapFailed>().Subscribe(failed =>
    {
        _statusText.text = $"Failed at {failed.StepName}";
        _retryButton.gameObject.SetActive(true);
    });
}

private void OnRetryClicked() => _bus.Publish(new BootstrapRetryRequested());
```

The re-run executes **every** step again, including ones that already succeeded — steps must be idempotent (see PITFALLS "Bootstrap step contract").

**Progress listening:** any UI can subscribe to `IBootstrapProgressReporter` without referencing the pipeline:

```csharp
[Inject] private IBootstrapProgressReporter _reporter;

private void OnEnable()
{
    _subscription = _reporter.Progress
        .Subscribe(progress => _loadingBar.value = progress);
}
```

## Step defaults (criticality / timeout / retries)

Defaults shipped by the template, in pipeline order.

**No shipped step is critical** (since 0.6.0). Bootstrap must never deny the player the game — a failed step degrades a feature and the pipeline runs on, recording the failure in `IBootstrapReport` and publishing `BootstrapStepDegraded`. `BootstrapDegradationTests.EveryShippedStep_IsNonCritical_SoBootstrapNeverBlocksEntry` pins this so it cannot regress.

`IsCritical` remains available for **consumer** steps that genuinely gate the game (a mandatory server login, say). It is not about importance — it answers "is the app unusable if this never initializes, *and* do I have a retry screen to show?" If the answer to the second half is no, leave it false; see "Why nothing is critical" below.

| # | Step | Phase | Timeout | MaxRetries | Note |
|---|---|---|---|---|---|
| 1 | Log | **blocking** | 10s | 1 | No-op; kept because step names are public API |
| 2 | DeviceProfile | **blocking** | 10s | 1 | Sync and free; targetFrameRate wants setting early |
| 3 | Save | **blocking** | 10s | 1 | First screen shows player state; two steps depend on it |
| 4 | Asset | **blocking** | 10s | **2** | First screen is content; catalog fetch is worth retrying |
| 5 | Crashlytics | deferred | **5s** | 1 | First of the deferred set |
| 6 | Consent | deferred | **none** | 1 | Real consent waits on a human — no deadline |
| 7 | RemoteConfig | deferred | 10s | 1 | Defaults readable before the fetch lands |
| 8 | Analytics | deferred | 10s | 1 | Real SDKs buffer pre-init events |
| 9 | Localization | deferred | 10s | 1 | Worst case is a brief flash of raw keys |
| 10 | Attribution | deferred | 10s | 1 | Needs the event in-session, not pre-frame-1 |
| 11 | Ads | deferred | 10s | 1 | Mediation init takes 1-5s |
| 12 | IAP | deferred | 10s | 1 | Store catalog fetch; gate the shop, not the game |
| 13 | Audio | deferred | 10s | 1 | Depends on Save, which is blocking |
| 14 | Time | deferred | 10s | 1 | Gate on ITimeService.IsServerSynced |
| 15 | Notification | deferred | 10s | 1 | Depends on Save, which is blocking |
| 16 | VersionCheck | deferred | 10s | 1 | Runs after RemoteConfig; declared order preserved |

### Phases

`IBootstrapStep.Phase` decides whether the player waits. `BootstrapStepBase.Phase` defaults to **`Blocking`** so an existing consumer step keeps the semantics it had before phases existed — but almost every step should be `Deferred`.

The blocking phase runs under a whole-phase budget (`BootstrapPipeline.DefaultBlockingBudget`, 5s). On expiry, unfinished and unstarted blocking steps are recorded degraded and `BootstrapReady` fires anyway — the budget is what bounds time-to-first-screen no matter how many blocking steps are added. Treat it as a circuit breaker, not a target.

The deferred phase runs **sequentially**, not concurrently. The tail costs the player nothing, and declared order preserves the one ordering edge inside the set (`RemoteConfig → VersionCheck`) without a dependency-declaration API and without interleaving services never written for it.

There are exactly three real ordering constraints in the whole pipeline, all verified from call sites: `Save → Audio`, `Save → Notification`, `RemoteConfig → VersionCheck`. Nine of the sixteen steps are islands nothing resolves at all.

`BootstrapPipeline.RunAsync` returns when the blocking phase ends. `DeferredCompletion` observes the rest; nothing needs to await it to play.

### Why nothing is critical

An abort has nowhere good to land. The pipeline stops, publishes `BootstrapFailed`, and `GameLauncher` writes one `Debug.LogError` — and **nothing in the template subscribes to `BootstrapFailed`**; `LoadingScreenView` has no failure path. The retry UI the design assumes is consumer work that no sample provides.

So in a stock project "critical" does not mean a controlled stop. It means the splash screen sits there forever. For a hybrid-casual or puzzle game, where time-to-first-interaction decides retention, that is the worst available outcome — and pre-0.6.0 it could be triggered by "quality settings failed to apply".

Continuing is not the same as swallowing. A step that exhausts its retries is recorded in `IBootstrapReport` and announced as `BootstrapStepDegraded`, so the game can switch off the feature that depends on it:

```csharp
[Inject] private IBootstrapReport _report;

private void Start()
{
    _shopButton.gameObject.SetActive(!_report.IsDegraded("IAP"));
}
```

Use the bus event to react at the moment of failure; use the report to ask after the fact. `R3EventBus` does not replay, so anything constructed after boot has already missed the event — which is why the durable record exists alongside it. The report is cleared at the start of every run, so a successful retry clears the degraded state.

**Consent carries an obligation.** Making `ConsentStep` non-critical means the game runs when the consent form could not load. The legal duty is "do not track without consent", not "do not run without consent" — so whatever you bind for ads / analytics / attribution must default to **non-personalized** when consent is unresolved. Check `IBootstrapReport.IsDegraded("Consent")` before enabling personalization.

**When you swap a real SDK into a step's service, re-review that step's `IsCritical` and `Timeout`.** Mocks return instantly, so the defaults have never been exercised against real network behavior in your project — a hanging vendor SDK consumes the full timeout on your splash screen. See PITFALLS "Swapping a real SDK into a bootstrap step".

## Examples

**Step lifecycle with retry:**
```csharp
public sealed class AssetsStep : BootstrapStepBase
{
    public override string Name => "Assets";
    public override bool IsCritical => true;      // No game without assets
    public override int MaxRetries => 2;

    protected override async UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
    {
        // If this throws on attempt 1, pipeline retries (attempt 2).
        // If attempt 2 throws, pipeline is critical so it aborts.
        await _assetService.InitializeAsync(ct);
    }
}
```

**Timeout + cancellation handling:**
```csharp
public sealed class NetworkStep : BootstrapStepBase
{
    public override TimeSpan Timeout => TimeSpan.FromSeconds(5);

    protected override async UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
    {
        // Pipeline wraps this in CancelAfter(Timeout).
        // If fetch takes >5s, ct is cancelled mid-await.
        // OperationCanceledException is caught by pipeline and retried (or aborted if critical).
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(Timeout);
        await FetchRemoteConfigAsync(cts.Token);
    }
}
```

## Known Limitations

- **Sequential only:** no parallel step execution. This is intentional for simplicity and deterministic ordering. If startup is slow, move expensive I/O into step-agnostic lazy init (load assets on demand, not at boot).
- **No conditional skipping:** all steps always run. To skip a step conditionally, check a flag inside `OnExecuteAsync` and return early.
- **Timeout applies to whole step:** if your step spawns background tasks, they won't be auto-cancelled when timeout fires. Use the provided `ct` token and respect it.
- **Retry re-runs everything:** `BootstrapRetryRequested` re-runs the whole pipeline; there is no completed-step tracking. Steps must be idempotent — guard one-time side effects (`if (_initialized) return;`) inside the service, not the step.

## Design Rationale

**Why a pipeline instead of individual service registration?** Because bootstrap order matters: Save must load before Settings, Localization must load before UI, Ads must load before Revenue tracking. Hard-coding order in `ProjectScopeInstaller.InstallBindings` (the explicit step list) makes dependencies transparent to code review, unlike automatic DI ordering which can surprise you.

**Why `IBootstrapProgressReporter` instead of exposing `BootstrapPipeline.Progress` directly?** Both are registered `Lifetime.Singleton, Resolution.Lazy`, so this is not about availability — it's about what a view drags into existence. Resolving `BootstrapPipeline` constructs its whole graph: all 16 steps and every service they depend on. A loading bar that injected the pipeline just to read a float would force that construction from the view layer, at whatever moment the view happens to resolve. The reporter is a cheap write-sink with a read-only contract (`Observable<float>`, `Observable<string>`); the pipeline writes to it, views read from it, and `GameLauncher` stays the only thing that resolves and runs the pipeline.

**Timeout strategy:** each step runs inside a linked CancellationTokenSource that fires `CancelAfter(step.Timeout)`. This is a soft timeout (requests to stop), not a hard kill. If a step ignores cancellation, it keeps running. In practice, most Unity async code (UniTask, Addressables) respects the token, so 30s default is safe for most network + I/O.

**Retry semantics:** non-critical steps are retried up to `MaxRetries` times on any exception except `OperationCanceledException` (which always propagates as a system signal, not a step failure), then recorded as degraded and passed. Critical steps are never retried; one failure aborts the pipeline.
