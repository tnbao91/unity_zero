# Scene Service

## Overview

`ISceneService` loads and unloads scenes by Addressables key and publishes load/unload events on R3 streams. The shipped impl is `AddressableSceneService` (`Packages/com.tnbao91.nobody.zero/Runtime/Services/Scene/AddressableSceneService.cs`) — a **real** service wrapping `Addressables.LoadSceneAsync`.

It exists so scene transitions go through one place: handles are tracked and released (no leaked `SceneInstance`), the previously-loaded scene is released automatically on a single-mode load, and any system can react to scene changes via `OnSceneLoaded` / `OnSceneUnloaded` without coupling to the loader.

## Public API

```csharp
namespace Zero.Core
{
    public interface ISceneService
    {
        string ActiveScene { get; }
        UniTask LoadAsync(string sceneKey, IProgress<float> progress = null, CancellationToken ct = default);
        UniTask UnloadAsync(string sceneKey, CancellationToken ct = default);
        Observable<string> OnSceneLoaded { get; }
        Observable<string> OnSceneUnloaded { get; }
    }
}
```

| Member | Behavior |
|---|---|
| `ActiveScene` | Key of the most recently loaded scene (`null` after it is unloaded). |
| `LoadAsync` | Loads `sceneKey` in `LoadSceneMode.Single` (activate on load), reporting `0..1` progress. **Throws** `ArgumentException` on null/empty key, `InvalidOperationException` on failure. On success, previously-tracked scenes are released (each emitting `OnSceneUnloaded`). |
| `UnloadAsync` | Unloads a tracked scene. If the key isn't tracked it **warns and no-ops** (fail-safe). Emits `OnSceneUnloaded` on completion. |
| `OnSceneLoaded` / `OnSceneUnloaded` | `Observable<string>` of scene keys. Backed by R3 `Subject`s; subscribe with `using R3;` at the top of the file. |

## Extension Points

The impl is `sealed`. Swap the binding in `SceneServiceInstaller.cs` (or your consumer partial) for a fake/alternate loader, or decorator-wrap to add a transition curtain:

```csharp
builder.RegisterType(
    typeof(FadeSceneService),
    new[] { typeof(ISceneService) },
    Lifetime.Singleton,
    Resolution.Lazy);
```

A curtain decorator forwards to the inner service between fade-out and fade-in:

```csharp
public sealed class FadeSceneService : ISceneService
{
    private readonly ISceneService _inner;
    public FadeSceneService(ISceneService inner) => _inner = inner;

    public async UniTask LoadAsync(string key, IProgress<float> p = null, CancellationToken ct = default)
    {
        await FadeOut();
        await _inner.LoadAsync(key, p, ct);
        await FadeIn();
    }
    // forward the rest
}
```

## Examples

Load a gameplay scene with a progress bar and react to completion:

```csharp
_scenes.OnSceneLoaded
    .Where(key => key == "scene/level")
    .Subscribe(_ => _events.Publish(new LevelSceneReady()))
    .AddTo(this);

await _scenes.LoadAsync("scene/level", progress: loadingBar, ct);
```

## Known Limitations

- **Single-mode only.** `LoadAsync` always uses `LoadSceneMode.Single`; additive scene loading is not exposed. Add an overload in a decorator/replacement if your game needs additive scenes.
- **`ActiveScene` tracks the loader's last single-load**, not Unity's `SceneManager.GetActiveScene()`. They agree for the template's single-scene flow; if you load additively outside this service they can diverge.
- **No double-load guard.** Loading the same key twice loads it twice; the service tracks both handles. Check `ActiveScene` first if you want load-once semantics.

## Design Rationale

- **Key-addressed, not path/build-index.** Only `Bootstrap.unity` is in Build Settings (see `CLAUDE.md` → consumer owns scenes); everything else ships as an Addressable, so the service speaks keys.
- **Auto-release of the previous scene.** Single-mode loads implicitly replace; releasing the old handle on the next load prevents the most common scene leak without the caller having to pair every load with an unload.
- **R3 events over callbacks.** Multiple unrelated systems (analytics, music, gameplay) need scene-change notifications; an `Observable<string>` lets each subscribe independently, with `using R3;` to bind the right `Subscribe` overload (see `docs/dev/PITFALLS.md`).
