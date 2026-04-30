# Level Loading

## Overview

`LevelLoader` uses `IAssetService` to load level prefabs via Addressables keys. It returns both the instantiated GameObject and a refcounted handle for explicit release. `ILevelDefinition` is an abstract ScriptableObject base for consumers to subclass with genre-specific fields.

## Public API

```csharp
public abstract class ILevelDefinition : ScriptableObject
{
    public abstract string Id { get; }
    public abstract string DisplayName { get; }
    public abstract string AddressablePrefabKey { get; }
}

public sealed class LevelLoader
{
    public async UniTask<(GameObject Instance, IAssetHandle<GameObject> Handle)> LoadLevelAsync(
        string addressableKey,
        CancellationToken ct = default);
}
```

### Lifetime
- `LevelLoader` is not injected as a service singleton; consumer instantiates as needed or wraps in their own play-state controller.
- Asset handles **must** be disposed to release refcounts. Forgetting to dispose leaks memory.

## Extension Points

### Custom Level Definition
Subclass `ILevelDefinition` and add genre-specific fields:

```csharp
public sealed class GridLevelDefinition : ILevelDefinition
{
    [SerializeField] private string _id;
    [SerializeField] private string _displayName;
    [SerializeField] private string _prefabKey;
    [SerializeField] private int _gridWidth;
    [SerializeField] private int _gridHeight;
    [SerializeField] private int _targetScore;

    public override string Id => _id;
    public override string DisplayName => _displayName;
    public override string AddressablePrefabKey => _prefabKey;

    public int GridWidth => _gridWidth;
    public int GridHeight => _gridHeight;
    public int TargetScore => _targetScore;
}
```

Author instances as `.asset` files and load them via `IAssetService`:

```csharp
var levelDefHandle = await _assetService.LoadAsync<ILevelDefinition>("levels/grid_001", ct);
var levelDef = levelDefHandle.Asset;
```

### Custom Level Instantiation
Wrap `LevelLoader` in a consumer's play-state coordinator:

```csharp
public sealed class PlayState : IGameState
{
    private readonly IAssetService _assetService;
    private GameObject _levelInstance;
    private IAssetHandle<GameObject> _levelHandle;

    public PlayState(IAssetService assetService)
    {
        _assetService = assetService;
    }

    public async UniTask EnterAsync(CancellationToken ct)
    {
        var loader = new LevelLoader(_assetService);
        (_levelInstance, _levelHandle) = await loader.LoadLevelAsync("levels/current", ct);

        // Initialize level-specific logic
        var controller = _levelInstance.GetComponent<LevelController>();
        controller.Start();
    }

    public async UniTask ExitAsync(CancellationToken ct)
    {
        // Cleanup
        if (_levelInstance != null)
            Object.Destroy(_levelInstance);

        // Release asset refcount
        _levelHandle?.Dispose();
    }

    public void Tick(float deltaTime)
    {
    }
}
```

## Examples

### Minimal Level Load
```csharp
var loader = new LevelLoader(_assetService);
var (levelGo, handle) = await loader.LoadLevelAsync("levels/level_001");
try
{
    levelGo.SetActive(true);
    // ... play
}
finally
{
    Object.Destroy(levelGo);
    handle.Dispose();
}
```

### Level Definition Asset
In editor, create a new `GridLevelDefinition` asset:
- Save as `Assets/_Project/Content/Levels/level_001.asset`
- Addressables group: mark with key `levels/level_001`
- Inspector: fill Id, DisplayName, prefab key, grid dimensions, target score

Load at runtime:
```csharp
var levelDefHandle = await _assetService.LoadAsync<GridLevelDefinition>("levels/level_001", ct);
var def = levelDefHandle.Asset;

var levelHandle = await _assetService.LoadAsync<GameObject>(def.AddressablePrefabKey, ct);
var instance = Object.Instantiate(levelHandle.Asset);
```

## Known Limitations

- **No prefab validation** — `LoadLevelAsync` does not check that the loaded prefab contains expected components (e.g., a `LevelController`). Consumer must validate or catch `NullReferenceException` on `GetComponent<T>`.
- **Single-load only** — `LevelLoader` loads once and returns. If you need to reload the same level, create a fresh `LevelLoader` instance and handle asset caching yourself (or wrap in `IAssetService` pre-check + conditional load).
- **No cancellation cleanup** — if `LoadLevelAsync` is cancelled mid-load, the returned handle is still valid and must be disposed (async operations never partially fail in `IAssetService`).
- **Instantiation is synchronous** — `Object.Instantiate` happens after the async load, so a huge prefab may frame-stutter on instantiation. Use Addressables' built-in instantiation if frame-time matters (see Addressables documentation).

## Design Rationale

**Why separate `ILevelDefinition` from prefab?** Metadata (id, display name, difficulty curve) and geometry (prefab) often change independently. ScriptableObject separation lets a level designer iterate on the prefab without re-authoring metadata.

**Why return both instance and handle?** Asset refcounting is explicit in this template. Consumer sees the contract: "you got a handle, you must dispose it." Hiding it in a wrapper increases the chance of leaks (e.g., wrapper disposed but prefab instance kept alive).

**Why not use Addressables.InstantiateAsync directly?** It's an option for consumers who prefer Addressables' async instantiation. This template keeps `LevelLoader` simple (load the prefab, you instantiate); consumer can swap it out for Addressables' direct instantiation if frame-time profiling shows a bottleneck.
