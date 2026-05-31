# Asset Service

## Overview

`IAssetService` is the Addressables wrapper the whole template loads content through. The shipped impl is `AddressableAssetService` (`Packages/com.tnbao91.nobody.zero/Runtime/Services/Asset/AddressableAssetService.cs`) — a **real** service (not a mock), since Addressables is a Unity-shipped package.

It exists so nothing in the game calls `Addressables.LoadAssetAsync` directly. Direct calls leak `AsyncOperationHandle`s (no central release), throw the red `InvalidKeyException` *before* your try/catch can see it, and make load-counting / leak-detection impossible. This service hands out a disposable `IAssetHandle<T>`, tracks every live handle, and dumps anything still held at shutdown.

## Public API

```csharp
namespace Zero.Core
{
    public interface IAssetHandle<T> : IDisposable where T : UnityEngine.Object
    {
        T Asset { get; }
        bool IsLoaded { get; }
    }

    public interface IAssetService
    {
        int ActiveHandleCount { get; }
        UniTask InitializeAsync(CancellationToken ct = default);
        UniTask<IAssetHandle<T>> LoadAsync<T>(string key, CancellationToken ct = default) where T : UnityEngine.Object;
        UniTask<bool> HasKeyAsync<T>(string key, CancellationToken ct = default) where T : UnityEngine.Object;
        UniTask PreloadAsync(IReadOnlyList<string> keys, IProgress<float> progress = null, CancellationToken ct = default);
    }
}
```

| Member | Behavior |
|---|---|
| `ActiveHandleCount` | Live (not-yet-disposed) handle count. Use it in tests/diagnostics to assert no leaks. |
| `InitializeAsync` | Idempotent `Addressables.InitializeAsync()`. Safe to call twice. |
| `LoadAsync<T>` | Loads `key`, returns a tracked `IAssetHandle<T>`. **Throws** `ArgumentException` on null/empty key, `InvalidOperationException` on load failure. The underlying op is released automatically if loading throws. |
| `HasKeyAsync<T>` | **Fail-safe** existence check via `LoadResourceLocationsAsync` — returns `false` for a missing key, never throws `InvalidKeyException`. Call this before `LoadAsync` when the key may not exist. |
| `PreloadAsync` | Warms a batch of keys (held internally, deduped), reporting `0..1` progress. A failed key warns and is skipped, not thrown. |

`AddressableAssetService.Unpreload(string key)` releases a single preloaded key (on the concrete type, not the interface).

## Extension Points

The impl is `sealed`. To substitute (e.g. an editor fake, a Resources-based fallback, or an instrumentation decorator), swap the binding in `AssetServiceInstaller.cs` or in your consumer `ProjectScopeInstaller.UserServices.cs` partial:

```csharp
builder.RegisterType(
    typeof(MyInstrumentedAssetService),
    new[] { typeof(IAssetService) },
    Lifetime.Singleton,
    Resolution.Lazy);
```

A decorator that logs every load is the common case:

```csharp
public sealed class LoggingAssetService : IAssetService
{
    private readonly IAssetService _inner;
    public LoggingAssetService(IAssetService inner) => _inner = inner;
    // forward each member, wrapping LoadAsync with timing/log
}
```

## Examples

Load → use → dispose (the handle owns the lifetime):

```csharp
using var handle = await _assets.LoadAsync<Sprite>("ui/icon/coin", ct);
icon.sprite = handle.Asset;
// handle.Dispose() on scope exit releases the Addressable + drops ActiveHandleCount
```

Guard an optional key:

```csharp
if (await _assets.HasKeyAsync<GameObject>(skinKey, ct))
{
    using var skin = await _assets.LoadAsync<GameObject>(skinKey, ct);
    Instantiate(skin.Asset);
}
```

Preload a level's assets with a progress bar:

```csharp
await _assets.PreloadAsync(level.AssetKeys, progress: loadingBar, ct);
```

## Known Limitations

- **Handle ownership is the caller's.** Forget to `Dispose()` (or `using`) and the asset stays resident until app quit, where it's logged as a leak. `ActiveHandleCount` is your tripwire.
- **`PreloadAsync` holds a separate retention set** from `LoadAsync` handles. Preloaded keys are released only by `Unpreload` or at shutdown — `LoadAsync` of a preloaded key still returns its own tracked handle.
- **No reference-count merging.** Two `LoadAsync` of the same key produce two independent handles / two Addressables acquisitions; this is intentional (simple ownership) but not the cheapest for hot-shared assets — preload those instead.

## Design Rationale

- **Disposable handle over raw key.** Tying the Addressable's lifetime to an `IDisposable` makes `using` the natural, leak-proof pattern and gives a single release path.
- **`HasKeyAsync` is fail-safe by contract.** Addressables logs the red exception *before* a surrounding try/catch runs (see `docs/dev/PITFALLS.md` → "Addressables logs the red exception BEFORE your try/catch sees it"), so existence is checked via `LoadResourceLocationsAsync`, which resolves to an empty list instead of throwing.
- **Shutdown leak dump.** `Application.quitting` releases everything still held and warns per-key — turning silent leaks into a visible console line during development.
