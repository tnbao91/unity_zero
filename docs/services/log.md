# Log Service

## Overview

`ILogService` is the template's logging seam. The shipped impl is `LogService` (`Packages/com.tnbao91.nobody.zero/Runtime/Services/Log/LogService.cs`) — a thin, **real** wrapper over `UnityEngine.Debug` with a single global on/off switch.

It exists so framework services log through an injected interface instead of calling `Debug.Log` directly. That gives one place to mute logs (release builds, tests), and a clean swap point to route logs to a crash reporter, an in-game console, or a file without touching every call site. Most services in the template (`AddressableAssetService`, `AddressableSceneService`, `DefaultAdPlacementService`, …) take `ILogService` in their constructor.

## Public API

```csharp
namespace Zero.Core
{
    public interface ILogService
    {
        bool IsEnabled { get; set; }
        void Info(string message);
        void Warn(string message);
        void Error(string message);
        void Error(Exception exception, string context = null);
    }
}
```

| Member | `LogService` behavior |
|---|---|
| `IsEnabled` | Master gate (default `true`). When `false`, every method is a no-op. |
| `Info` | `Debug.Log` |
| `Warn` | `Debug.LogWarning` |
| `Error(string)` | `Debug.LogError` |
| `Error(Exception, context)` | Logs `context` as an error first (if non-empty), then `Debug.LogException`. |

## Extension Points

The impl is `sealed`. Swap the binding in `LogServiceInstaller.cs` (or your consumer partial) to route logs elsewhere:

```csharp
builder.RegisterType(
    typeof(CrashlyticsLogService),
    new[] { typeof(ILogService) },
    Lifetime.Singleton,
    Resolution.Lazy);
```

Common replacements / decorators:
- **Crash-reporter tee** — forward `Warn`/`Error` to `ICrashlyticsService.Log` (see `docs/services/crashlytics.md`) while keeping `Debug` output.
- **In-game console** — also push lines into the dev cheat console (`docs/dev/cheat-console.md`).
- **Severity filter** — a decorator that drops `Info` in release but keeps `Warn`/`Error`.

```csharp
public sealed class TeeLogService : ILogService
{
    private readonly ILogService _inner;
    private readonly ICrashlyticsService _crash;
    public TeeLogService(ILogService inner, ICrashlyticsService crash) { _inner = inner; _crash = crash; }
    public bool IsEnabled { get => _inner.IsEnabled; set => _inner.IsEnabled = value; }
    public void Info(string m) => _inner.Info(m);
    public void Warn(string m) { _inner.Warn(m); _crash.Log(m); }
    public void Error(string m) { _inner.Error(m); _crash.Log(m); }
    public void Error(Exception e, string c = null) { _inner.Error(e, c); _crash.RecordException(e); }
}
```

## Examples

```csharp
public sealed class MyService
{
    private readonly ILogService _log;
    public MyService(ILogService log) => _log = log;

    public void Foo()
    {
        _log.Info("[MY] starting");
        try { Risky(); }
        catch (Exception e) { _log.Error(e, "[MY] Foo failed"); }
    }
}
```

Mute logs in an EditMode test:

```csharp
var log = new LogService { IsEnabled = false };
```

## Known Limitations

- **`IsEnabled` is all-or-nothing.** No per-severity or per-tag filtering — a replacement impl adds that if needed.
- **No structured fields.** Messages are plain strings; for structured/queryable logs, route through a replacement that formats to JSON or your telemetry SDK.
- **No build-config stripping.** Calls still execute (and allocate the message string) when `IsEnabled` is `false`; the work is just discarded. For per-frame hot paths, gate the call site, not just the service — see `CLAUDE.md` → "Allocate per-frame in Update".

## Design Rationale

- **Interface, not `Debug` everywhere.** A seam at the boundary lets release builds mute logs and lets crash/console routing be a one-line binding swap instead of a project-wide find-replace.
- **Wraps Unity's logger, not a custom one.** Per the template's "prefer Unity-default packages" principle, the default impl is the smallest possible passthrough — no custom log pipeline to maintain.
- **`Error(Exception, context)` overload.** Pairing a human-readable context line with `Debug.LogException` keeps the stack trace intact while still telling you *which* operation failed.
