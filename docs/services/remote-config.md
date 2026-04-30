# Remote Config Service

## Overview

`IRemoteConfigService` is a key-value store for runtime feature flags, A/B variants, and live-ops constants like `min_version`. The template ships `MockRemoteConfigService` — an in-memory `Dictionary<string, object>` with no fetch behavior. `RemoteConfigStep` calls `FetchAndActivateAsync` at bootstrap so subsequent steps (e.g. `VersionCheckStep`) can `TryGetString` the activated values.

Real impls wrap Firebase Remote Config or Unity Remote Config.

## Public API

```csharp
namespace Zero.Core
{
    public interface IRemoteConfigService
    {
        UniTask<bool> FetchAndActivateAsync(TimeSpan timeout, CancellationToken ct = default);
        T GetVariant<T>(string key, T defaultValue);
        bool TryGetString(string key, out string value);
        bool TryGetLong(string key, out long value);
        bool TryGetDouble(string key, out double value);
        bool TryGetBool(string key, out bool value);
        Observable<Unit> OnConfigUpdated { get; }
    }
}
```

## Mock behavior

`MockRemoteConfigService` (`Assets/_Project/Scripts/Runtime/Services/RemoteConfig/MockRemoteConfigService.cs`):
- All `TryGet*` calls return `false` for any key (empty store).
- `FetchAndActivateAsync` returns `true` immediately.
- `GetVariant<T>` returns `defaultValue`.
- `OnConfigUpdated` never emits.

To seed mock values for local testing, add `Set*` helpers in your fork or use a simple wrapper that pre-populates on init.

## Extension Points

### Swap to Firebase Remote Config

1. Install Firebase Remote Config via UPM ([docs](https://firebase.google.com/docs/remote-config/get-started?platform=unity)).
2. Add `Firebase.RemoteConfig` to `Zero.Services.RemoteConfig.asmdef` references.
3. Implement `FirebaseRemoteConfigService : IRemoteConfigService`:

```csharp
using Firebase.RemoteConfig;

public sealed class FirebaseRemoteConfigService : IRemoteConfigService
{
    private readonly Subject<Unit> _updated = new();
    public Observable<Unit> OnConfigUpdated => _updated;

    public async UniTask<bool> FetchAndActivateAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        var task = FirebaseRemoteConfig.DefaultInstance.FetchAsync(timeout);
        await task.AsUniTask().Timeout(timeout);
        var ok = await FirebaseRemoteConfig.DefaultInstance.ActivateAsync().AsUniTask();
        if (ok) _updated.OnNext(Unit.Default);
        return ok;
    }

    public bool TryGetString(string key, out string value)
    {
        var v = FirebaseRemoteConfig.DefaultInstance.GetValue(key);
        value = v.Source == ValueSource.RemoteValue ? v.StringValue : null;
        return value != null;
    }

    // ... TryGetLong / TryGetDouble / TryGetBool symmetrically.

    public T GetVariant<T>(string key, T defaultValue)
    {
        // Map T → Firebase ConfigValue accessor.
        if (typeof(T) == typeof(string) && TryGetString(key, out var s)) return (T)(object)s;
        if (typeof(T) == typeof(bool) && TryGetBool(key, out var b)) return (T)(object)b;
        return defaultValue;
    }
}
```

4. Replace the binding in `RemoteConfigServiceInstaller.cs`:
```csharp
builder.RegisterType(typeof(FirebaseRemoteConfigService), new[] { typeof(IRemoteConfigService) }, Lifetime.Singleton, Resolution.Lazy);
```

5. Configure Firebase Remote Config defaults in `RemoteConfigStep` so the service has fallback values during the first fetch.

### Swap to Unity Remote Config

[Unity Remote Config](https://docs.unity3d.com/Packages/com.unity.remote-config@latest) requires a Unity Cloud project and authentication via Unity Authentication. The wrapper is similar to Firebase — `RemoteConfigService.Instance.FetchConfigsAsync(...)` returns the values, then `TryGet*` reads from the activated config.

### A/B variants

Real impls expose conditions / segments in the dashboard. Use `GetVariant<T>("variant_key", "control")` to read which arm the user is in:

```csharp
var spawnRate = _remoteConfig.GetVariant<double>("enemy_spawn_rate", 1.0);
```

Variant assignment is sticky per user (driven by Firebase's `installation_id`); no client-side bookkeeping required.

## Examples

Read the maintenance flag (used by `VersionCheckService`):
```csharp
if (_remoteConfig.TryGetBool("maintenance_mode", out var maintenance) && maintenance)
{
    // ... show maintenance popup.
}
```

Subscribe to live config updates (Firebase pushes on second fetch):
```csharp
_remoteConfig.OnConfigUpdated.Subscribe(_ => RefreshLevelDifficulty());
```

## Known Limitations

- **Mock has no Set helpers.** `MockRemoteConfigService` doesn't expose mutation publicly; tests should construct a stub with `Set*` methods (see `VersionCheckServiceTests` for the pattern).
- **No "fetch and use" race protection.** If gameplay reads `TryGetBool` before `FetchAndActivateAsync` completes, the call returns `false` regardless of remote state. The template's `RemoteConfigStep` runs early, but if you call from a thread before bootstrap finishes, you'll see stale data. Defensively: don't read remote-config in service ctors; read at use site.
- **Variant typing is constrained** to string / long / double / bool. Complex JSON variants need to be returned as strings and parsed client-side.

## Design Rationale

- **`Try*` pattern** matches `Dictionary<TKey, TValue>.TryGetValue`. The service either has a real value or it doesn't; consumer always provides a fallback at the call site.
- **Both `TryGetString` AND `GetVariant<T>`** because some reads are "I want the value if it exists" (live-ops gates) and some are "I always want a value, fall back to default" (variant assignment). Two methods, two semantics.
- **`OnConfigUpdated`** is `Observable<Unit>` — subscribers re-read the values they care about. Pushing a typed payload through the bus would force every subscriber to filter, defeating R3's per-stream-per-event pattern.
