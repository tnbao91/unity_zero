# Analytics Service

## Overview

`IAnalyticsService` records gameplay events (`level_started`, `iap_purchased`, etc.) and user properties (`country`, `tutorial_completed`). The template ships `MockAnalyticsService` that prints every call to `Debug.Log` so you can verify event names + parameters during development.

Real impls wrap Firebase Analytics, GameAnalytics, Tenjin, or any combination (most production games dispatch to multiple SDKs).

## Public API

```csharp
namespace Zero.Core
{
    public interface IAnalyticsService
    {
        UniTask InitializeAsync(CancellationToken ct = default);
        void LogEvent(string eventName);
        void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters);
        void SetUserProperty(string name, string value);
        void SetUserId(string userId);
    }
}
```

## Mock behavior

`MockAnalyticsService` (`Assets/_Project/Scripts/Runtime/Services/Analytics/MockAnalyticsService.cs`) — every `LogEvent` writes a formatted line like `[Analytics] level_started {level=5, difficulty=hard}` to `Debug.Log`. `SetUserProperty` and `SetUserId` log similarly. `InitializeAsync` is a no-op.

## Extension Points

### Swap to Firebase Analytics

1. Install Firebase Analytics via UPM.
2. Add `Firebase.Analytics` to `Zero.Services.Analytics.asmdef` references.
3. Implement `FirebaseAnalyticsService : IAnalyticsService`:

```csharp
using Firebase.Analytics;

public sealed class FirebaseAnalyticsService : IAnalyticsService
{
    public UniTask InitializeAsync(CancellationToken ct = default)
    {
        // Firebase auto-initializes via FirebaseApp.
        return UniTask.CompletedTask;
    }

    public void LogEvent(string eventName)
        => FirebaseAnalytics.LogEvent(eventName);

    public void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters)
    {
        var fb = new Parameter[parameters.Count];
        var i = 0;
        foreach (var (k, v) in parameters)
        {
            fb[i++] = v switch
            {
                long l => new Parameter(k, l),
                double d => new Parameter(k, d),
                _ => new Parameter(k, v?.ToString() ?? string.Empty),
            };
        }
        FirebaseAnalytics.LogEvent(eventName, fb);
    }

    public void SetUserProperty(string name, string value)
        => FirebaseAnalytics.SetUserProperty(name, value);

    public void SetUserId(string userId)
        => FirebaseAnalytics.SetUserId(userId);
}
```

4. Replace the binding in `AnalyticsServiceInstaller.cs`:
```csharp
builder.RegisterType(typeof(FirebaseAnalyticsService), new[] { typeof(IAnalyticsService) }, Lifetime.Singleton, Resolution.Lazy);
```

### Multi-SDK fan-out

Most production games hit Firebase + GameAnalytics + an attribution SDK simultaneously. Wrap them in a composite:

```csharp
public sealed class CompositeAnalyticsService : IAnalyticsService
{
    private readonly IAnalyticsService[] _backends;

    public CompositeAnalyticsService(params IAnalyticsService[] backends) { _backends = backends; }

    public async UniTask InitializeAsync(CancellationToken ct = default)
    {
        foreach (var b in _backends) await b.InitializeAsync(ct);
    }

    public void LogEvent(string eventName)
    {
        foreach (var b in _backends) b.LogEvent(eventName);
    }
    // ... etc.
}
```

Bind via factory:
```csharp
builder.RegisterFactory<IAnalyticsService>(
    c => new CompositeAnalyticsService(
        new FirebaseAnalyticsService(),
        new GameAnalyticsService(),
        c.Resolve<IAttributionService>() as IAnalyticsService),
    new[] { typeof(IAnalyticsService) },
    Lifetime.Singleton, Resolution.Lazy);
```

### Subscribe to bus events for funnel logging

Pair analytics with `IEventBus` so feature code doesn't have to know about analytics:

```csharp
[Inject] private IEventBus _bus;
[Inject] private IAnalyticsService _analytics;

private void Awake()
{
    _bus.On<LevelStarted>().Subscribe(e => _analytics.LogEvent("level_started",
        new Dictionary<string, object> { ["level_id"] = e.LevelId }));

    _bus.On<LevelCompleted>().Subscribe(e => _analytics.LogEvent("level_completed",
        new Dictionary<string, object> { ["level_id"] = e.LevelId, ["score"] = e.Score }));
}
```

This keeps `LevelManager` analytics-agnostic.

## Examples

```csharp
[Inject] private IAnalyticsService _analytics;

private void OnTutorialFinished()
{
    _analytics.SetUserProperty("tutorial_completed", "true");
    _analytics.LogEvent("tutorial_complete", new Dictionary<string, object>
    {
        ["duration_sec"] = _stopwatch.ElapsedSeconds,
        ["skipped"] = false,
    });
}
```

## Known Limitations

- **Mock allocates per-event** (string concatenation for the log line). Switch to a real impl before benchmarking.
- **No batched send.** Each `LogEvent` calls the underlying SDK directly. Most SDKs batch internally.
- **Parameter values are `object`.** Some SDKs accept only string / number; the conversion happens inside your wrapper. Be explicit about types in your event spec.
- **No automatic consent gating.** Tie `SetUserId` and PII-bearing events to `IConsentService.GdprStatus == Personalized` in your wrapper.

## Design Rationale

- **Free-form `IReadOnlyDictionary<string, object>` for parameters** — every SDK has a different "structured event" model; lowest-common-denominator is key/value bag.
- **Single `LogEvent(name)` overload alongside the parameterized one** — common case is fire-and-forget event with no payload; forcing an empty dictionary is friction.
- **`SetUserId` on the analytics interface** rather than a separate `IUserSession` — every SDK that consumes events also keys off user-id; aligning the call sites avoids drift.
- **No `Flush` / `Forward` API** — those are SDK-internal optimizations. Surface them in your wrapper if you need explicit control (e.g. flush before `Application.Quit`).
