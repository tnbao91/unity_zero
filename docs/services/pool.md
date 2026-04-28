# Object Pool Service

## Overview

`IPoolService` provides reusable `ObjectPool<T>` instances for managing spawned GameObjects. The implementation `UnityPoolService` wraps `UnityEngine.Pool.ObjectPool<GameObject>` (built-in since Unity 2021+), using callbacks to automatically `SetActive(true)` on spawn and `SetActive(false)` on release. Pooled objects are parented under a `[Zero.Pools]` root to keep the hierarchy clean.

## Public API

```csharp
public interface IPoolService
{
    IObjectPool<T> GetPool<T>() where T : class;
}

// Usage via the built-in interface
public interface IObjectPool<T>
{
    T Get();
    void Release(T element);
}

// Implementation in Zero.Services.Pool
public sealed class UnityPoolService : IPoolService, IDisposable
{
    public const int DefaultCapacity = 10;
    public const int MaxSize = 10000;
}
```

## Extension Points

**Component pools:** while the interface is generic, the implementation specializes in `GameObject`. To pool `AudioSource` or `ParticleSystem` components, create a GameObject prefab with that component and pool the GameObject:

```csharp
// Instead of IPoolService.GetPool<AudioSource>():
var prefab = Resources.Load<GameObject>("Prefabs/AudioSourceGO");
var pool = _poolService.GetPool<GameObject>();
var audioSourceGO = pool.Get();
var audioSource = audioSourceGO.GetComponent<AudioSource>();
```

**Custom pool parameters:** if you need different capacity/maxSize per pool type, extend `UnityPoolService` and override `GetPool<T>()` to apply custom settings:

```csharp
public sealed class CustomPoolService : UnityPoolService
{
    public override IObjectPool<T> GetPool<T>()
    {
        var pool = base.GetPool<T>();
        // Customize capacity, maxSize, etc. via reflection if needed
        return pool;
    }
}
```

## Examples

**Spawn and release bullets:**
```csharp
[Inject] private IPoolService _pool;
private IObjectPool<GameObject> _bulletPool;

private void Start()
{
    _bulletPool = _pool.GetPool<GameObject>();
}

private void FireBullet(Vector3 position, Vector3 direction)
{
    var bullet = _bulletPool.Get();
    bullet.transform.position = position;
    bullet.GetComponent<Rigidbody>().velocity = direction * 10f;
}

public void OnBulletHit(GameObject bullet)
{
    bullet.GetComponent<Rigidbody>().velocity = Vector3.zero;
    _bulletPool.Release(bullet);
}
```

**VFX particle effects (fire-and-forget):**
```csharp
private IObjectPool<GameObject> _explosionPool;

private async UniTask ShowExplosion(Vector3 pos, CancellationToken ct)
{
    var vfx = _explosionPool.Get();
    vfx.transform.position = pos;
    var ps = vfx.GetComponent<ParticleSystem>();
    ps.Play();
    
    // Wait for particle duration
    await UniTask.Delay((int)(ps.main.duration * 1000), cancellationToken: ct);
    _explosionPool.Release(vfx);
}
```

## Known Limitations

- **No automatic release on destroy:** if you destroy a pooled object while released (e.g., via `Object.Destroy(go)` instead of pool.Release), the pool thinks it's still available and may return a destroyed reference on next `Get()`. Always release via the pool, not via `Destroy()`.
- **No cross-frame object reuse hints:** if you spawn 100 bullets per frame but only need 10, the pool will grow to 100. Periodic pruning (shrink the pool when not in use) is not implemented; if memory bloat is a concern, call `GetPool<T>().Clear()` manually or recreate the pool.
- **No priority/FIFO ordering:** `ObjectPool<T>` uses LIFO (last-out-first-in, stack-based), so the most recently released object is reused first. If you need FIFO ordering or prioritization, wrap the pool with a custom scheduler.

## Design Rationale

**Why `UnityEngine.Pool.ObjectPool` instead of a hand-rolled `Stack<T>`?** Because the built-in class includes `collectionCheck` (editor-only warnings for double-release), `maxSize` bounds (prevents unbounded growth), and standardized `actionOnGet`/`actionOnRelease`/`actionOnDestroy` callbacks. This is battle-tested infrastructure, and custom pooling is a common footgun.

**Callbacks for SetActive:**
- `actionOnGet`: called when `Get()` is invoked. We use this to `SetActive(true)` + reset transform.
- `actionOnRelease`: called when `Release()` is invoked. We use this to `SetActive(false)` + reparent to `[Zero.Pools]`.
- `actionOnDestroy`: called when the pool is disposed or an object exceeds maxSize. We use this to `Object.Destroy()`.

This design ensures spawned objects are active and at the requested position/rotation, without manual setup every time.

**Per-pool root:** all released (inactive) objects are reparented under `[Zero.Pools]` so the scene hierarchy doesn't get cluttered with inactive game objects. When spawned again, they're reparented to their game destination.

**Defaults:** `DefaultCapacity = 10` (initial pool size) and `MaxSize = 10000` (hard cap to prevent memory bloat). For most games, these are safe. Per-pool tuning is possible via subclassing if needed.
