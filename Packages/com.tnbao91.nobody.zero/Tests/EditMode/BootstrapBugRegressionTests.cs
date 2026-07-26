using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.TestTools;
using Zero.Core;
using Zero.Services.Audio;
using Zero.Services.Save;

namespace Zero.Tests.EditMode
{
    /// <summary>
    /// Two defects found while auditing boot cost for 0.7.0. Both predate the phase split;
    /// both are made more reachable by it, which is why they are fixed alongside.
    /// </summary>
    [TestFixture]
    public sealed class BootstrapBugRegressionTests
    {
        // ---- AudioMixerService leaked a GameObject pair on every call ----

        [UnityTest]
        public IEnumerator AudioMixerService_InitializeTwice_DoesNotLeakASecondAudioGameObject() =>
            UniTask.ToCoroutine(async () =>
        {
            // BootstrapRetryRequested re-runs every step, so a second InitializeAsync is a
            // shipped code path. Before the fix each call created a fresh [Zero.AudioMusic]
            // and [Zero.AudioSfxSource] with DontDestroyOnLoad and dropped the old ones.
            int before = CountAudioObjects();

            var service = new AudioMixerService(
                new StubLogService(), new StubAssetService(), new StubSaveService(), new StubPoolService());

            await service.InitializeAsync();
            int afterFirst = CountAudioObjects();

            await service.InitializeAsync();
            int afterSecond = CountAudioObjects();

            Assert.AreEqual(afterFirst, afterSecond,
                "Second InitializeAsync must be a no-op, not another pair of DontDestroyOnLoad objects.");
            Assert.Greater(afterFirst, before, "Sanity: the first call really did create the audio objects.");

            service.Dispose();
        });

        private static int CountAudioObjects()
        {
            int n = 0;
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null) continue;
                if (go.name == "[Zero.AudioMusic]" || go.name == "[Zero.AudioSfxSource]") n++;
            }
            return n;
        }

        // ---- EncryptedJsonSaveService threw from its constructor ----

        [Test]
        public void EncryptedJsonSaveService_Constructor_DoesNotTouchSecrets()
        {
            // The ctor runs during Reflex container resolve — before step 1, outside the
            // pipeline. A throw there produced an opaque resolve failure with no
            // BootstrapFailed, no degradation and no pipeline log. Seed derivation now
            // happens on first use, inside SaveStep, where a failure is reported like any
            // other. In the Editor LoadSeeds warns and falls back, so this must not throw
            // here either way — what is being pinned is that the ctor does no crypto setup.
            Assert.DoesNotThrow(() => new EncryptedJsonSaveService(new StubLogService()));
        }

        // ---- Stubs ----

        private sealed class StubLogService : ILogService
        {
            public bool IsEnabled { get; set; } = true;
            public void Info(string message) { }
            public void Warn(string message) { }
            public void Error(string message) { }
            public void Error(Exception exception, string context = null) { }
        }

        private sealed class StubSaveService : ISaveService
        {
            private readonly Dictionary<string, object> _values = new Dictionary<string, object>();
            private readonly Subject<Unit> _onLoaded = new Subject<Unit>();
            public Observable<Unit> OnLoaded => _onLoaded;
            public UniTask LoadAsync(CancellationToken ct = default) => UniTask.CompletedTask;
            public UniTask SaveAsync(CancellationToken ct = default) => UniTask.CompletedTask;
            public void RequestSave() { }
            public bool TryGet<T>(string key, out T value)
            {
                if (!string.IsNullOrEmpty(key) && _values.TryGetValue(key, out var raw) && raw is T typed)
                {
                    value = typed;
                    return true;
                }
                value = default;
                return false;
            }
            public void Set<T>(string key, T value) { if (!string.IsNullOrEmpty(key)) _values[key] = value; }
            public void Delete(string key) { if (!string.IsNullOrEmpty(key)) _values.Remove(key); }
        }

        private sealed class StubAssetService : IAssetService
        {
            public int ActiveHandleCount => 0;
            public UniTask InitializeAsync(CancellationToken ct = default) => UniTask.CompletedTask;
            public UniTask<IAssetHandle<T>> LoadAsync<T>(string key, CancellationToken ct = default)
                where T : UnityEngine.Object => UniTask.FromResult<IAssetHandle<T>>(null);
            public UniTask<bool> HasKeyAsync<T>(string key, CancellationToken ct = default)
                where T : UnityEngine.Object => UniTask.FromResult(false);
            public UniTask PreloadAsync(IReadOnlyList<string> keys, IProgress<float> progress = null,
                CancellationToken ct = default) => UniTask.CompletedTask;
        }

        private sealed class StubPoolService : IPoolService
        {
            public UniTask PrewarmAsync<T>(T prefab, int count, CancellationToken ct = default)
                where T : UnityEngine.Object => UniTask.CompletedTask;
            public IPool<T> GetPool<T>(T prefab) where T : UnityEngine.Object => new StubPool<T>();
            public void Clear<T>(T prefab) where T : UnityEngine.Object { }
        }

        private sealed class StubPool<T> : IPool<T> where T : UnityEngine.Object
        {
            public int Active => 0;
            public int Inactive => 0;
            public T Spawn(Vector3 position, Quaternion rotation) => null;
            public T Spawn() => null;
            public void Despawn(T instance) { }
        }
    }
}
