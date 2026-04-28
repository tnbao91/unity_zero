# Bootstrap Pipeline

## Overview

The bootstrap pipeline is a **sequential, resilient startup sequence** that initializes all services from a single, reorderable list. Each step (Crashlytics, Save, Assets, Localization, Ads, etc.) is optional, has configurable timeout/retry/criticality, and reports progress to a `IBootstrapProgressReporter` for UI display. Steps that fail non-critically are logged but don't block launch; critical steps abort the entire pipeline.

## Public API

```csharp
// In Zero.Infrastructure
public sealed class BootstrapPipeline
{
    public BootstrapPipeline(
        IBootstrapStep[] steps,
        ILogService log,
        IBootstrapProgressReporter progressReporter);
    
    public async UniTask RunAsync(CancellationToken ct);
}

// Base class in Zero.Infrastructure for all steps
public abstract class BootstrapStepBase : IBootstrapStep
{
    public abstract string Name { get; }
    public abstract bool IsCritical { get; }
    public virtual TimeSpan Timeout => TimeSpan.FromSeconds(30);
    public virtual int MaxRetries => 1;
    
    protected abstract UniTask OnExecuteAsync(CancellationToken ct);
}

// Reporting interface in Zero.Core
public interface IBootstrapProgressReporter
{
    Observable<float> Progress { get; }           // 0.0 to 1.0
    Observable<string> CurrentStepName { get; }   // "Loading Assets" etc.
}
```

## Extension Points

**Custom bootstrap steps:** inherit `BootstrapStepBase`, override `OnExecuteAsync`, and register in `ProjectScopeInstaller.InstallBindings()`:

```csharp
public sealed class MyCustomStep : BootstrapStepBase
{
    public override string Name => "MyCustom";
    public override bool IsCritical => false;      // Non-critical failures don't block
    public override TimeSpan Timeout => TimeSpan.FromSeconds(10);
    public override int MaxRetries => 3;
    
    protected override async UniTask OnExecuteAsync(CancellationToken ct)
    {
        // Init your service here. Throw if failed.
        // Pipeline will retry up to MaxRetries times (non-critical),
        // or abort (critical).
        await _myService.InitAsync(ct);
    }
}

// Then in ProjectScopeInstaller.cs, add to the steps array:
// new MyCustomStep(/*deps*/)
```

**Progress listening:** any UI can subscribe to `IBootstrapProgressReporter` without referencing the pipeline:

```csharp
[Inject] private IBootstrapProgressReporter _reporter;

private void OnEnable()
{
    _subscription = _reporter.Progress
        .Subscribe(progress => _loadingBar.value = progress);
}
```

## Examples

**Step lifecycle with retry:**
```csharp
public sealed class AssetsStep : BootstrapStepBase
{
    public override string Name => "Assets";
    public override bool IsCritical => true;      // No game without assets
    public override int MaxRetries => 2;
    
    protected override async UniTask OnExecuteAsync(CancellationToken ct)
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
    
    protected override async UniTask OnExecuteAsync(CancellationToken ct)
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

## Design Rationale

**Why a pipeline instead of individual service registration?** Because bootstrap order matters: Save must load before Settings, Localization must load before UI, Ads must load before Revenue tracking. Hard-coding order in `ProjectScopeInstaller.InstallBindings` (the explicit step list) makes dependencies transparent to code review, unlike automatic DI ordering which can surprise you.

**Why `IBootstrapProgressReporter` instead of exposing `BootstrapPipeline.Progress` directly?** Because the pipeline is `Lifetime.Singleton, Resolution.Lazy` — if a view tries to inject it directly, there's a race: the view might resolve before the pipeline is instantiated. The reporter is registered as `Singleton, Resolution.Eager`, so it's available immediately. Views read from the reporter, not the pipeline.

**Timeout strategy:** each step runs inside a linked CancellationTokenSource that fires `CancelAfter(step.Timeout)`. This is a soft timeout (requests to stop), not a hard kill. If a step ignores cancellation, it keeps running. In practice, most Unity async code (UniTask, Addressables) respects the token, so 30s default is safe for most network + I/O.

**Retry semantics:** non-critical steps are retried up to `MaxRetries` times on any exception except `OperationCanceledException` (which always propagates as a system signal, not a step failure). Critical steps are never retried; one failure aborts the pipeline.
