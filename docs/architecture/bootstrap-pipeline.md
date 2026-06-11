# Bootstrap Pipeline

## Overview

The bootstrap pipeline is a **sequential, resilient startup sequence** that initializes all services from a single, reorderable list. Each step (Crashlytics, Save, Assets, Localization, Ads, etc.) is optional, has configurable timeout/retry/criticality, and reports progress to a `IBootstrapProgressReporter` for UI display. Steps that fail non-critically are logged but don't block launch; critical steps abort the entire pipeline — the abort publishes `BootstrapFailed` on `IEventBus` and surfaces as `BootstrapStepFailedException`, and a consumer can publish `BootstrapRetryRequested` to make `GameLauncher` re-run the pipeline (see "Failure & retry" below).

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
        IEventBus eventBus = null);   // publishes BootstrapFailed on critical abort

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

Defaults shipped by the template, in pipeline order. `IsCritical` is **not** about importance — it answers "is the app unusable if this never initializes?" Ordering is a separate decision (Crashlytics runs first so later failures get reported, yet it is non-critical: aborting launch produces zero reports anyway).

| # | Step | IsCritical | Timeout | MaxRetries | Note |
|---|---|---|---|---|---|
| 1 | Crashlytics | false | **5s** | 1 | First for ordering; never blocks launch |
| 2 | Log | false | 30s | 1 | |
| 3 | DeviceProfile | **true** | 30s | 1 | Quality tiers gate everything after |
| 4 | Save | false | 30s | 1 | Service resets-to-empty internally |
| 5 | Asset | **true** | 30s | 1 | No game without Addressables |
| 6 | Consent | **true** | 30s | 1 | Legal gate for ads/analytics |
| 7 | RemoteConfig | false | 30s | 1 | |
| 8 | Analytics | false | 30s | 1 | |
| 9 | Localization | false | 30s | 1 | |
| 10 | Attribution | false | 30s | 1 | |
| 11 | Ads | false | 30s | 1 | |
| 12 | IAP | false | 30s | 1 | |
| 13 | Audio | false | 30s | 1 | |
| 14 | Time | false | 30s | 1 | |
| 15 | Notification | false | 30s | 1 | |
| 16 | VersionCheck | false | 30s | 1 | |

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

**Why `IBootstrapProgressReporter` instead of exposing `BootstrapPipeline.Progress` directly?** Because the pipeline is `Lifetime.Singleton, Resolution.Lazy` — if a view tries to inject it directly, there's a race: the view might resolve before the pipeline is instantiated. The reporter is registered as `Singleton, Resolution.Eager`, so it's available immediately. Views read from the reporter, not the pipeline.

**Timeout strategy:** each step runs inside a linked CancellationTokenSource that fires `CancelAfter(step.Timeout)`. This is a soft timeout (requests to stop), not a hard kill. If a step ignores cancellation, it keeps running. In practice, most Unity async code (UniTask, Addressables) respects the token, so 30s default is safe for most network + I/O.

**Retry semantics:** non-critical steps are retried up to `MaxRetries` times on any exception except `OperationCanceledException` (which always propagates as a system signal, not a step failure). Critical steps are never retried; one failure aborts the pipeline.
