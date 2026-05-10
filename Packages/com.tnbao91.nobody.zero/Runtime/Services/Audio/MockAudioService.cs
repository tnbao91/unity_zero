using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zero.Core;

namespace Zero.Services.Audio
{
    public sealed class MockAudioService : IAudioService
    {
        private readonly ILogService _log;
        private readonly Dictionary<AudioBus, float> _vols = new()
        {
            [AudioBus.Master] = 1f,
            [AudioBus.Music] = 0.7f,
            [AudioBus.Sfx] = 0.8f,
            [AudioBus.Ui] = 0.6f,
            [AudioBus.Voice] = 1f,
        };

        public MockAudioService(ILogService log)
        {
            _log = log;
        }

        public UniTask InitializeAsync(CancellationToken ct = default)
        {
            _log.Info("[AUDIO:mock] Initialized");
            return UniTask.CompletedTask;
        }

        public UniTask PlayMusicAsync(string clipKey, bool loop = true, CancellationToken ct = default)
        {
            _log.Info($"[AUDIO:mock] Music '{clipKey}' loop={loop}");
            return UniTask.CompletedTask;
        }

        public void StopMusic()
        {
            _log.Info("[AUDIO:mock] StopMusic");
        }

        public UniTask PlaySfxAsync(string clipKey, CancellationToken ct = default)
        {
            _log.Info($"[AUDIO:mock] SFX '{clipKey}'");
            return UniTask.CompletedTask;
        }

        public void SetBusVolume(AudioBus bus, float volume)
        {
            _vols[bus] = Mathf.Clamp01(volume);
        }

        public float GetBusVolume(AudioBus bus) => _vols.TryGetValue(bus, out var v) ? v : 1f;
    }
}
