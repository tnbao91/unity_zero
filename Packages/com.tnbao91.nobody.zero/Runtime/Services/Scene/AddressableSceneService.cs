using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using Zero.Core;

namespace Zero.Services.Scene
{
    public sealed class AddressableSceneService : ISceneService, IDisposable
    {
        private readonly ILogService _log;
        private readonly Dictionary<string, AsyncOperationHandle<SceneInstance>> _active = new();
        private readonly Subject<string> _onLoaded = new();
        private readonly Subject<string> _onUnloaded = new();
        private readonly object _lock = new();
        private string _activeScene;
        private bool _disposed;

        public string ActiveScene => _activeScene;
        public Observable<string> OnSceneLoaded => _onLoaded;
        public Observable<string> OnSceneUnloaded => _onUnloaded;

        public AddressableSceneService(ILogService log)
        {
            _log = log;
        }

        public async UniTask LoadAsync(string sceneKey, IProgress<float> progress = null, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(sceneKey))
                throw new ArgumentException("Scene key must be non-empty.", nameof(sceneKey));
            _log.Info($"[SCENE] Loading '{sceneKey}'...");
            var op = Addressables.LoadSceneAsync(sceneKey, LoadSceneMode.Single, activateOnLoad: true);
            await op.ToUniTask(progress: progress, cancellationToken: ct);

            if (op.Status != AsyncOperationStatus.Succeeded)
            {
                var msg = op.OperationException?.Message ?? "unknown";
                throw new InvalidOperationException($"[SCENE] Load failed for '{sceneKey}': {msg}");
            }

            ReleasePreviousHandles(sceneKey);

            lock (_lock) { _active[sceneKey] = op; }
            _activeScene = sceneKey;
            _log.Info($"[SCENE] Loaded '{sceneKey}'");
            _onLoaded.OnNext(sceneKey);
        }

        public async UniTask UnloadAsync(string sceneKey, CancellationToken ct = default)
        {
            AsyncOperationHandle<SceneInstance> op;
            lock (_lock)
            {
                if (!_active.TryGetValue(sceneKey, out op))
                {
                    _log.Warn($"[SCENE] No tracked handle for '{sceneKey}'");
                    return;
                }
                _active.Remove(sceneKey);
            }

            _log.Info($"[SCENE] Unloading '{sceneKey}'");
            var unload = Addressables.UnloadSceneAsync(op);
            await unload.ToUniTask(cancellationToken: ct);
            if (_activeScene == sceneKey) _activeScene = null;
            _onUnloaded.OnNext(sceneKey);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            List<AsyncOperationHandle<SceneInstance>> snapshot;
            lock (_lock)
            {
                snapshot = new List<AsyncOperationHandle<SceneInstance>>(_active.Values);
                _active.Clear();
            }
            foreach (var op in snapshot)
            {
                if (op.IsValid()) Addressables.Release(op);
            }
            _onLoaded.Dispose();
            _onUnloaded.Dispose();
        }

        private void ReleasePreviousHandles(string keepKey)
        {
            List<KeyValuePair<string, AsyncOperationHandle<SceneInstance>>> toRelease = null;
            lock (_lock)
            {
                foreach (var kv in _active)
                {
                    if (kv.Key == keepKey) continue;
                    toRelease ??= new List<KeyValuePair<string, AsyncOperationHandle<SceneInstance>>>();
                    toRelease.Add(kv);
                }
                if (toRelease != null)
                {
                    foreach (var kv in toRelease) _active.Remove(kv.Key);
                }
            }

            if (toRelease == null) return;
            foreach (var kv in toRelease)
            {
                if (kv.Value.IsValid()) Addressables.Release(kv.Value);
                _onUnloaded.OnNext(kv.Key);
            }
        }
    }
}
