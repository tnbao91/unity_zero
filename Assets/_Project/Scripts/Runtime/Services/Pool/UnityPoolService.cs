using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;
using Zero.Core;
using Object = UnityEngine.Object;

namespace Zero.Services.Pool
{
    public sealed class UnityPoolService : IPoolService, IDisposable
    {
        private readonly ILogService _log;
        private readonly Dictionary<int, IPoolHandle> _pools = new();
        private Transform _root;
        private bool _disposed;

        public UnityPoolService(ILogService log)
        {
            _log = log;
        }

        public async UniTask PrewarmAsync<T>(T prefab, int count, CancellationToken ct = default) where T : Object
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            var pool = ResolveGameObjectPool(prefab);
            for (int i = 0; i < count; i++)
            {
                ct.ThrowIfCancellationRequested();
                pool.Prewarm();
                if (i % 8 == 0) await UniTask.Yield(ct); // breathe
            }
        }

        public IPool<T> GetPool<T>(T prefab) where T : Object
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            var go = ResolveGameObjectPool(prefab);
            if (typeof(T) == typeof(GameObject)) return (IPool<T>)(object)go;

            int key = prefab.GetInstanceID();
            int wrapKey = unchecked(key ^ typeof(T).GetHashCode());
            if (_pools.TryGetValue(wrapKey, out var existing)) return (IPool<T>)existing;

            if (typeof(Component).IsAssignableFrom(typeof(T)))
            {
                var wrapper = new ComponentPool<T>(go);
                _pools[wrapKey] = wrapper;
                return wrapper;
            }
            throw new InvalidOperationException($"[POOL] T={typeof(T).Name} not supported. Use GameObject or Component subclass.");
        }

        public void Clear<T>(T prefab) where T : Object
        {
            if (prefab == null) return;
            int key = prefab.GetInstanceID();
            if (_pools.TryGetValue(key, out var pool))
            {
                pool.Dispose();
                _pools.Remove(key);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var p in _pools.Values) p.Dispose();
            _pools.Clear();
            if (_root != null) Object.Destroy(_root.gameObject);
        }

        private GameObjectPool ResolveGameObjectPool<T>(T prefab) where T : Object
        {
            GameObject prefabGo = prefab as GameObject ?? (prefab as Component)?.gameObject;
            if (prefabGo == null) throw new InvalidOperationException($"[POOL] T={typeof(T).Name} is not a GameObject or Component");

            int key = prefabGo.GetInstanceID();
            if (_pools.TryGetValue(key, out var existing)) return (GameObjectPool)existing;

            EnsureRoot();
            var pool = new GameObjectPool(prefabGo, _root, _log);
            _pools[key] = pool;
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
                Object.Destroy(go);
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

            internal void Prewarm()
            {
                var inst = _pool.Get();
                _pool.Release(inst);
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
