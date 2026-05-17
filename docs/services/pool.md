# Object Pool Service

## Overview

`IPoolService` provides per-prefab `IPool<T>` instances for spawning and despawning Unity `GameObject`s and component subclasses. The implementation `UnityPoolService` wraps `UnityEngine.Pool.ObjectPool<GameObject>` (built-in since Unity 2021), so allocations stay bounded and `collectionCheck` catches double-release in the Editor. Released instances are reparented under a `[Zero.Pools]` `DontDestroyOnLoad` root to keep scenes clean.

## Public API

```csharp
namespace Zero.Core
{
    public interface IPool<T> where T : UnityEngine.Object
    {
        int Active { get; }
        int Inactive { get; }
        T Spawn();
        T Spawn(Vector3 position, Quaternion rotation);
        void Despawn(T instance);
    }

    public interface IPoolService
    {
        UniTask PrewarmAsync<T>(T prefab, int count, CancellationToken ct = default)
            where T : UnityEngine.Object;
        IPool<T> GetPool<T>(T prefab) where T : UnityEngine.Object;
        void Clear<T>(T prefab) where T : UnityEngine.Object;
    }
}
```

`Spawn` activates the instance after parenting + positioning (so `OnEnable` sees the requested transform). `Despawn` deactivates and reparents to the pool root.

## Extension Points

- **Component pools:** call `GetPool<MyComponent>(prefab)` where `prefab` is a `GameObject` *or* the component itself; the inner `ComponentPool<T>` wraps the GameObject pool and resolves the component on each spawn.
- **Custom defaults:** the `UnityEngine.Pool.ObjectPool<GameObject>` is created in `UnityPoolService.GameObjectPool` with `defaultCapacity: 10`, `maxSize: 10000`, and Editor-only `collectionCheck`. `UnityPoolService` is `sealed`; per-game tuning means swapping the binding in `PoolServiceInstaller` for a full `IPoolService` replacement implementation with the constants you want.
- **Replace entirely:** rebind `IPoolService` to a different impl in your own `<Game>ScopeInstaller.UserServices.cs` partial — Reflex picks the last registration.

## Examples

```csharp
public sealed class BulletSpawner : MonoBehaviour
{
    [Inject] private IPoolService _pools;
    [SerializeField] private GameObject _bulletPrefab;
    private IPool<GameObject> _pool;

    private void Awake() => _pool = _pools.GetPool(_bulletPrefab);

    public void Fire(Vector3 origin, Quaternion aim)
    {
        var bullet = _pool.Spawn(origin, aim);
        // ...physics setup...
    }

    public void OnBulletExpired(GameObject bullet) => _pool.Despawn(bullet);
}
```

Prewarm at scene load to avoid first-shot hitches:

```csharp
await _pools.PrewarmAsync(_bulletPrefab, count: 32, ct);
```

## Known Limitations

- **`Despawn` of a `Destroy`d object is undefined.** Always despawn through the pool; never `Object.Destroy(...)` a pooled instance, or the pool will hand a destroyed reference back on the next `Spawn`.
- **No automatic shrink.** Pools grow up to `maxSize` and stay there. If a level burst-spawns 500 instances and the next level uses 5, you keep the high-water memory until you `Clear(prefab)` or dispose the service.
- **LIFO reuse.** `Spawn` returns the most recently `Despawn`ed instance first (stack semantics from `UnityEngine.Pool.ObjectPool`). If you depend on FIFO, wrap the pool yourself.
- **No thread safety.** All pool ops must happen on the Unity main thread.

## Design Rationale

The original `ReflexPoolService` (renamed to `UnityPoolService` in Phase 1a) hand-rolled a `Stack<GameObject>` for inactive storage. That predated `UnityEngine.Pool.ObjectPool`, which now offers the same primitives plus `collectionCheck`, `maxSize`, and matured callback hooks. Switching dropped ~30 lines of bespoke code and gave us double-release detection in the Editor for free.

`actionOnGet` is deliberately left **null**: activation happens in `Spawn(...)` *after* the instance is reparented and positioned, so `OnEnable` callbacks see the requested transform. The earlier ordering — `SetActive(true)` inside `actionOnGet` — caused `OnEnable` to fire while the object was still parked at the pool root with stale position. Fixed in commit `c0ee281`.

`createFunc` instantiates each new GameObject parented to `[Zero.Pools]` and immediately `SetActive(false)`, so freshly-created instances never flash active in the wrong place between create-and-first-Spawn.
