using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Zero.Core;

namespace Zero.Services.Asset
{
    public sealed class AddressableAssetService : IAssetService, IDisposable
    {
        private readonly ILogService _log;
        private readonly Dictionary<int, ScopedAddressableHandle> _activeHandles = new();
        private readonly Dictionary<string, AsyncOperationHandle> _preloaded = new();
        private readonly object _lock = new();

        private int _nextHandleId;
        private bool _initialized;
        private bool _disposed;

        public int ActiveHandleCount
        {
            get { lock (_lock) return _activeHandles.Count; }
        }

        public AddressableAssetService(ILogService log)
        {
            _log = log;
            Application.quitting += OnQuitting;
        }

        public async UniTask InitializeAsync(CancellationToken ct = default)
        {
            if (_initialized) return;
            _log.Info("[ASSET] Initializing Addressables...");
            await Addressables.InitializeAsync().ToUniTask(cancellationToken: ct);
            _initialized = true;
            _log.Info("[ASSET] Addressables ready");
        }

        public async UniTask<IAssetHandle<T>> LoadAsync<T>(string key, CancellationToken ct = default)
            where T : UnityEngine.Object
        {
            var op = Addressables.LoadAssetAsync<T>(key);
            try
            {
                await op.ToUniTask(cancellationToken: ct);
            }
            catch
            {
                if (op.IsValid()) Addressables.Release(op);
                throw;
            }

            if (op.Status != AsyncOperationStatus.Succeeded)
            {
                var msg = op.OperationException?.Message ?? "unknown";
                if (op.IsValid()) Addressables.Release(op);
                throw new InvalidOperationException($"[ASSET] Load failed for '{key}': {msg}");
            }

            int id;
            ScopedAddressableHandle scoped;
            lock (_lock)
            {
                id = ++_nextHandleId;
                scoped = new ScopedAddressableHandle(this, id, op, key, _log);
                _activeHandles[id] = scoped;
            }
            return new TypedHandle<T>(scoped, op);
        }

        public async UniTask<bool> HasKeyAsync<T>(string key, CancellationToken ct = default) where T : UnityEngine.Object
        {
            // Resolves to empty list if no entry — never throws InvalidKeyException.
            var op = Addressables.LoadResourceLocationsAsync(key, typeof(T));
            try
            {
                await op.ToUniTask(cancellationToken: ct);
                return op.Status == AsyncOperationStatus.Succeeded && op.Result != null && op.Result.Count > 0;
            }
            finally
            {
                if (op.IsValid()) Addressables.Release(op);
            }
        }

        public async UniTask PreloadAsync(IReadOnlyList<string> keys, IProgress<float> progress = null, CancellationToken ct = default)
        {
            if (keys == null || keys.Count == 0) return;
            int done = 0;
            foreach (var key in keys)
            {
                ct.ThrowIfCancellationRequested();
                bool already;
                lock (_lock) { already = _preloaded.ContainsKey(key); }
                if (already)
                {
                    progress?.Report(++done / (float)keys.Count);
                    continue;
                }

                var op = Addressables.LoadAssetAsync<UnityEngine.Object>(key);
                try
                {
                    await op.ToUniTask(cancellationToken: ct);
                }
                catch
                {
                    if (op.IsValid()) Addressables.Release(op);
                    throw;
                }

                if (op.Status == AsyncOperationStatus.Succeeded)
                {
                    lock (_lock) { _preloaded[key] = op; }
                }
                else
                {
                    if (op.IsValid()) Addressables.Release(op);
                    _log.Warn($"[ASSET] Preload failed for '{key}'");
                }
                progress?.Report(++done / (float)keys.Count);
            }
        }

        public void Unpreload(string key)
        {
            AsyncOperationHandle op;
            lock (_lock)
            {
                if (!_preloaded.Remove(key, out op)) return;
            }
            if (op.IsValid()) Addressables.Release(op);
        }

        internal void NotifyHandleDisposed(int id)
        {
            lock (_lock) { _activeHandles.Remove(id); }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Application.quitting -= OnQuitting;
            DumpAndReleaseAll();
        }

        private void OnQuitting() => DumpAndReleaseAll();

        private void DumpAndReleaseAll()
        {
            List<ScopedAddressableHandle> snapshotActive;
            List<KeyValuePair<string, AsyncOperationHandle>> snapshotPreload;
            lock (_lock)
            {
                snapshotActive = new List<ScopedAddressableHandle>(_activeHandles.Values);
                snapshotPreload = new List<KeyValuePair<string, AsyncOperationHandle>>(_preloaded);
                _activeHandles.Clear();
                _preloaded.Clear();
            }

            if (snapshotActive.Count > 0)
            {
                _log.Warn($"[ASSET] {snapshotActive.Count} handle(s) leaked at shutdown");
                foreach (var h in snapshotActive)
                {
                    _log.Warn($"[ASSET]   leaked: {h.Key}");
                    h.ReleaseSilent();
                }
            }

            foreach (var kv in snapshotPreload)
            {
                if (kv.Value.IsValid()) Addressables.Release(kv.Value);
            }
        }
    }

    internal sealed class ScopedAddressableHandle
    {
        private readonly AddressableAssetService _owner;
        private readonly int _id;
        private readonly ILogService _log;
        private AsyncOperationHandle _op;
        private bool _disposed;

        public string Key { get; }

        public ScopedAddressableHandle(AddressableAssetService owner, int id, AsyncOperationHandle op, string key, ILogService log)
        {
            _owner = owner;
            _id = id;
            _op = op;
            Key = key;
            _log = log;
        }

        public bool IsLoaded => !_disposed && _op.IsValid() && _op.IsDone && _op.Status == AsyncOperationStatus.Succeeded;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_op.IsValid()) Addressables.Release(_op);
            _owner.NotifyHandleDisposed(_id);
        }

        internal void ReleaseSilent()
        {
            if (_disposed) return;
            _disposed = true;
            if (_op.IsValid()) Addressables.Release(_op);
        }
    }

    internal sealed class TypedHandle<T> : IAssetHandle<T> where T : UnityEngine.Object
    {
        private readonly ScopedAddressableHandle _inner;
        private readonly AsyncOperationHandle<T> _typed;

        public TypedHandle(ScopedAddressableHandle inner, AsyncOperationHandle<T> typed)
        {
            _inner = inner;
            _typed = typed;
        }

        public T Asset => _typed.IsValid() ? _typed.Result : null;
        public bool IsLoaded => _inner.IsLoaded;
        public void Dispose() => _inner.Dispose();
    }
}
