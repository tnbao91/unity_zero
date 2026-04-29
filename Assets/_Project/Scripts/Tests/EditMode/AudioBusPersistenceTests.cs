using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;
using UnityEngine;
using Zero.Core;

namespace Zero.Tests.EditMode
{
    /// <summary>
    /// Tests audio bus volume persistence via ISaveService.
    /// Uses an in-memory stub save service (no real mixer needed).
    /// </summary>
    public sealed class AudioBusPersistenceTests
    {
        private StubSaveService _saveService;
        private StubLogService _logService;
        private StubAssetService _assetService;
        private StubPoolService _poolService;

        [SetUp]
        public void Setup()
        {
            _saveService = new StubSaveService();
            _logService = new StubLogService();
            _assetService = new StubAssetService();
            _poolService = new StubPoolService();
        }

        [Test]
        public async UniTask SetBusVolume_PersistsToSaveService()
        {
            var service = new MockAudioServiceForTesting(
                _logService, _assetService, _saveService, _poolService);
            await service.InitializeAsync();

            // Set Master bus to 0.5
            service.SetBusVolume(AudioBus.Master, 0.5f);

            // Verify saved
            Assert.IsTrue(_saveService.TryGet("audio.bus.master", out float saved));
            Assert.AreEqual(0.5f, saved, 0.01f);
        }

        [Test]
        public async UniTask GetBusVolume_ReadsFromCache()
        {
            var service = new MockAudioServiceForTesting(
                _logService, _assetService, _saveService, _poolService);
            await service.InitializeAsync();

            service.SetBusVolume(AudioBus.Music, 0.7f);
            float retrieved = service.GetBusVolume(AudioBus.Music);

            Assert.AreEqual(0.7f, retrieved, 0.01f);
        }

        [Test]
        public async UniTask SecondInstance_LoadsPersisted()
        {
            // First instance: set volume
            var service1 = new MockAudioServiceForTesting(
                _logService, _assetService, _saveService, _poolService);
            await service1.InitializeAsync();
            service1.SetBusVolume(AudioBus.Sfx, 0.3f);

            // Second instance: should load persisted value
            var service2 = new MockAudioServiceForTesting(
                _logService, _assetService, _saveService, _poolService);
            await service2.InitializeAsync();
            float retrieved = service2.GetBusVolume(AudioBus.Sfx);

            Assert.AreEqual(0.3f, retrieved, 0.01f);
        }

        [Test]
        public async UniTask SetBusVolume_Clamps01()
        {
            var service = new MockAudioServiceForTesting(
                _logService, _assetService, _saveService, _poolService);
            await service.InitializeAsync();

            service.SetBusVolume(AudioBus.Ui, 1.5f);
            float retrieved = service.GetBusVolume(AudioBus.Ui);

            Assert.AreEqual(1f, retrieved, 0.01f);
        }

        /// <summary>In-memory save service for testing (no encryption).</summary>
        private sealed class StubSaveService : ISaveService
        {
            private readonly Dictionary<string, object> _data = new();
            private readonly Subject<Unit> _onLoaded = new();

            public Observable<Unit> OnLoaded => _onLoaded;

            public UniTask LoadAsync(CancellationToken ct = default)
            {
                return UniTask.CompletedTask;
            }

            public UniTask SaveAsync(CancellationToken ct = default)
            {
                return UniTask.CompletedTask;
            }

            public void RequestSave()
            {
            }

            public bool TryGet<T>(string key, out T value)
            {
                if (_data.TryGetValue(key, out var obj) && obj is T typedValue)
                {
                    value = typedValue;
                    return true;
                }
                value = default;
                return false;
            }

            public void Set<T>(string key, T value)
            {
                _data[key] = value;
            }

            public void Delete(string key)
            {
                _data.Remove(key);
            }
        }

        /// <summary>Stub log service for testing.</summary>
        private sealed class StubLogService : ILogService
        {
            public bool IsEnabled { get; set; } = true;
            public void Info(string message) { }
            public void Warn(string message) { }
            public void Error(string message) { }
            public void Error(Exception exception, string context = null) { }
        }

        /// <summary>Stub asset service for testing.</summary>
        private sealed class StubAssetService : IAssetService
        {
            public int ActiveHandleCount => 0;

            public UniTask InitializeAsync(CancellationToken ct = default)
            {
                return UniTask.CompletedTask;
            }

            public UniTask<IAssetHandle<T>> LoadAsync<T>(string key, CancellationToken ct = default) where T : UnityEngine.Object
            {
                return UniTask.FromResult<IAssetHandle<T>>(null);
            }

            public UniTask PreloadAsync(IReadOnlyList<string> keys, IProgress<float> progress = null, CancellationToken ct = default)
            {
                return UniTask.CompletedTask;
            }
        }

        /// <summary>Stub pool service for testing.</summary>
        private sealed class StubPoolService : IPoolService
        {
            public UniTask PrewarmAsync<T>(T prefab, int count, CancellationToken ct = default) where T : UnityEngine.Object
            {
                return UniTask.CompletedTask;
            }

            public IPool<T> GetPool<T>(T prefab) where T : UnityEngine.Object
            {
                return new StubPool<T>();
            }

            public void Clear<T>(T prefab) where T : UnityEngine.Object
            {
            }
        }

        /// <summary>Stub pool implementation.</summary>
        private sealed class StubPool<T> : IPool<T> where T : UnityEngine.Object
        {
            public int Active => 0;
            public int Inactive => 0;

            public T Spawn(Vector3 position, Quaternion rotation) => null;
            public T Spawn() => null;
            public void Despawn(T instance) { }
        }

        /// <summary>Minimal mock audio service using stubs (no real mixer).</summary>
        private sealed class MockAudioServiceForTesting
        {
            private readonly ILogService _log;
            private readonly IAssetService _assetService;
            private readonly ISaveService _saveService;
            private readonly IPoolService _poolService;
            private readonly Dictionary<AudioBus, float> _busVolumes = new();

            public MockAudioServiceForTesting(
                ILogService log, IAssetService assetService, ISaveService saveService, IPoolService poolService)
            {
                _log = log;
                _assetService = assetService;
                _saveService = saveService;
                _poolService = poolService;
            }

            public async UniTask InitializeAsync(CancellationToken ct = default)
            {
                // Load persisted bus volumes
                foreach (AudioBus bus in System.Enum.GetValues(typeof(AudioBus)))
                {
                    string key = $"audio.bus.{bus.ToString().ToLowerInvariant()}";
                    float defaultVolume = GetDefaultVolumeForBus(bus);
                    if (_saveService.TryGet(key, out float savedVolume))
                    {
                        _busVolumes[bus] = Mathf.Clamp01(savedVolume);
                    }
                    else
                    {
                        _busVolumes[bus] = defaultVolume;
                    }
                }
                await UniTask.CompletedTask;
            }

            public void SetBusVolume(AudioBus bus, float volume)
            {
                float clamped = Mathf.Clamp01(volume);
                _busVolumes[bus] = clamped;

                string key = $"audio.bus.{bus.ToString().ToLowerInvariant()}";
                _saveService.Set(key, clamped);
            }

            public float GetBusVolume(AudioBus bus)
            {
                return _busVolumes.TryGetValue(bus, out var v) ? v : 1f;
            }

            private static float GetDefaultVolumeForBus(AudioBus bus)
            {
                return bus switch
                {
                    AudioBus.Master => 1f,
                    AudioBus.Music => 0.7f,
                    AudioBus.Sfx => 0.8f,
                    AudioBus.Ui => 0.6f,
                    AudioBus.Voice => 1f,
                    _ => 1f
                };
            }
        }
    }

}
