using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;
using Zero.Core;
using Zero.Infrastructure;
using Object = UnityEngine.Object;

namespace Zero.Services.Pool
{
    public sealed class UnityPoolService : IPoolService, IDisposable
    {
        private readonly ILogService _log;
        private readonly Dictionary<EntityId, GameObjectPool> _goPools = new();
        private readonly Dictionary<(EntityId, Type), IPoolHandle> _wrapPools = new();
        private Transform _root;
        private bool _disposed;

        public UnityPoolService(ILogService log)
        {
            _log = log;
        }

        public async UniTask PrewarmAsync<T>(T prefab, int count, CancellationToken ct = default) where T : Object
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            if (count <= 0) return;

            var pool = ResolveGameObjectPool(prefab);
            const int chunkSize = 8;
            int remaining = count;
            while (remaining > 0)
            {
                ct.ThrowIfCancellationRequested();
                int batch = Math.Min(chunkSize, remaining);
                pool.Prewarm(batch);
                remaining -= batch;

                // Yield between chunks in play mode to avoid frame hitches when
                // prewarming large pools. Skip in EditMode — UniTask.Yield's
                // default PlayerLoopTiming.Update does not tick in editor
                // scripts, so the await would never resume cleanly.
                if (Application.isPlaying && remaining > 0)
                {
                    await UniTask.Yield(ct);
                }
            }
        }

        public IPool<T> GetPool<T>(T prefab) where T : Object
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            var go = ResolveGameObjectPool(prefab);
            if (typeof(T) == typeof(GameObject)) return (IPool<T>)(object)go;

            var wrapKey = (prefab.GetEntityId(), typeof(T));
            if (_wrapPools.TryGetValue(wrapKey, out var existing)) return (IPool<T>)existing;

            if (typeof(Component).IsAssignableFrom(typeof(T)))
            {
                var wrapper = new ComponentPool<T>(go);
                _wrapPools[wrapKey] = wrapper;
                return wrapper;
            }
            throw new InvalidOperationException($"[POOL] T={typeof(T).Name} not supported. Use GameObject or Component subclass.");
        }

        public void Clear<T>(T prefab) where T : Object
        {
            if (prefab == null) return;
            EntityId id = prefab.GetEntityId();
            if (_goPools.TryGetValue(id, out var goPool)) { goPool.Dispose(); _goPools.Remove(id); }
            var wrapKey = (id, typeof(T));
            if (_wrapPools.TryGetValue(wrapKey, out var wrap)) { wrap.Dispose(); _wrapPools.Remove(wrapKey); }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var p in _goPools.Values) p.Dispose();
            foreach (var p in _wrapPools.Values) p.Dispose();
            _goPools.Clear();
            _wrapPools.Clear();
            if (_root != null) Util.SafeDestroy(_root.gameObject);
        }

        private GameObjectPool ResolveGameObjectPool<T>(T prefab) where T : Object
        {
            GameObject prefabGo = prefab as GameObject ?? (prefab as Component)?.gameObject;
            if (prefabGo == null) throw new InvalidOperationException($"[POOL] T={typeof(T).Name} is not a GameObject or Component");

            EntityId key = prefabGo.GetEntityId();
            if (_goPools.TryGetValue(key, out var existing)) return existing;

            EnsureRoot();
            var pool = new GameObjectPool(prefabGo, _root, _log);
            _goPools[key] = pool;
            return pool;
        }

        private void EnsureRoot()
        {
            if (_root != null) return;
            var go = new GameObject("[Zero.Pools]");
            // DontDestroyOnLoad throws InvalidOperationException in EditMode
            // (Editor scripts and EditMode tests). At runtime the pool root
            // must survive scene loads; in the Editor we just leave it parented
            // to the active scene — good enough for tests and tooling.
            if (Application.isPlaying)
            {
                Object.DontDestroyOnLoad(go);
            }
            go.SetActive(true);
            _root = go.transform;
        }

        private interface IPoolHandle : IDisposable { }

        private sealed class GameObjectPool : IPool<GameObject>, IPoolHandle
        {
            private readonly GameObject _prefab;
            private readonly Transform _root;
            private readonly ILogService _log;
            private ObjectPool<GameObject> _pool;

            public int Active => _pool.CountActive;
            public int Inactive => _pool.CountInactive;

            public GameObjectPool(GameObject prefab, Transform root, ILogService log)
            {
                _prefab = prefab;
                _root = root;
                _log = log;

                // Create UnityEngine.Pool.ObjectPool with appropriate actions
#if UNITY_EDITOR
                const bool collectionCheck = true;
#else
                const bool collectionCheck = false;
#endif
                _pool = new ObjectPool<GameObject>(
                    createFunc: CreateGameObject,
                    actionOnGet: null,
                    actionOnRelease: OnRelease,
                    actionOnDestroy: OnDestroy,
                    collectionCheck: collectionCheck,
                    defaultCapacity: 10,
                    maxSize: 10000
                );
            }

            private GameObject CreateGameObject()
            {
                var inst = Object.Instantiate(_prefab, _root, false);
                inst.SetActive(false);
                return inst;
            }

            private void OnRelease(GameObject go)
            {
                go.SetActive(false);
                go.transform.SetParent(_root, false);
            }

            private void OnDestroy(GameObject go)
            {
                Util.SafeDestroy(go);
            }

            public GameObject Spawn() => Spawn(Vector3.zero, Quaternion.identity);

            public GameObject Spawn(Vector3 position, Quaternion rotation)
            {
                var inst = _pool.Get();
                inst.transform.SetParent(null, false);
                inst.transform.SetPositionAndRotation(position, rotation);
                inst.SetActive(true);
                return inst;
            }

            public void Despawn(GameObject instance)
            {
                if (instance == null) return;
                _pool.Release(instance);
            }

            // Forces createFunc to run `count` times. Naively looping
            // Get → Release would only ever produce ONE instance because each
            // Get pops the just-released one from the internal stack. We have
            // to hold them all out simultaneously, then release them in one
            // go so the pool ends up with `count` inactive instances.
            internal void Prewarm(int count)
            {
                if (count <= 0) return;
                var held = new GameObject[count];
                for (int i = 0; i < count; i++) held[i] = _pool.Get();
                for (int i = 0; i < count; i++) _pool.Release(held[i]);
            }

            public void Dispose()
            {
                _pool?.Clear();
                _pool?.Dispose();
                _pool = null;
            }
        }

        private sealed class ComponentPool<T> : IPool<T>, IPoolHandle where T : Object
        {
            private readonly GameObjectPool _inner;

            public ComponentPool(GameObjectPool inner) => _inner = inner;

            public int Active => _inner.Active;
            public int Inactive => _inner.Inactive;

            public T Spawn() => Spawn(Vector3.zero, Quaternion.identity);

            public T Spawn(Vector3 position, Quaternion rotation)
            {
                var go = _inner.Spawn(position, rotation);
                return (go as T) ?? go.GetComponent(typeof(T)) as T;
            }

            public void Despawn(T instance)
            {
                if (instance is GameObject go) _inner.Despawn(go);
                else if (instance is Component comp) _inner.Despawn(comp.gameObject);
            }

            public void Dispose() => _inner.Dispose();
        }
    }
}
