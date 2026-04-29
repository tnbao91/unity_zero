using System;
using System.Collections.Generic;
using System.Threading;
using AnnulusGames.LitMotion;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;
using Zero.Core;

namespace Zero.Services.Audio
{
    /// <summary>
    /// Real audio service wrapping AudioMixer (loaded via Addressables at key "audio/main_mixer").
    /// Buses: Master, Music, Sfx, Ui, Voice with exposed parameters Bus{Name} (e.g., "BusMaster").
    /// Music: persistent source with crossfade. SFX: pooled one-shot sources.
    /// Bus volumes persisted via ISaveService (keys: audio.bus.{bus}).
    /// </summary>
    public sealed class AudioMixerService : IAudioService, IDisposable
    {
        private readonly ILogService _log;
        private readonly IAssetService _assetService;
        private readonly ISaveService _saveService;
        private readonly IPoolService _poolService;
        private readonly Dictionary<AudioBus, float> _busVolumes = new();

        private AudioMixer _mixer;
        private AudioSource _musicSource;
        private GameObject _musicSourceGo;
        private IPool<AudioSource> _sfxPool;
        private bool _disposed;

        public AudioMixerService(ILogService log, IAssetService assetService, ISaveService saveService, IPoolService poolService)
        {
            _log = log;
            _assetService = assetService;
            _saveService = saveService;
            _poolService = poolService;
        }

        public async UniTask InitializeAsync(CancellationToken ct = default)
        {
            try
            {
                // Load mixer from Addressables
                _mixer = await _assetService.LoadAsync<AudioMixer>("audio/main_mixer", ct);
            }
            catch (Exception ex)
            {
                _log.Warn($"[AUDIO] Failed to load mixer from 'audio/main_mixer': {ex.Message}. Falling back to default volumes.");
                _mixer = null;
            }

            // Initialize bus volume cache from save service
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

            // Create root GameObject for music source
            GameObject rootGo = new GameObject("[Zero.AudioMusic]")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            if (Application.isPlaying)
            {
                Object.DontDestroyOnLoad(rootGo);
            }

            _musicSourceGo = rootGo;
            _musicSource = _musicSourceGo.AddComponent<AudioSource>();
            _musicSource.loop = true;
            if (_mixer != null)
            {
                _musicSource.outputAudioMixerGroup = GetMixerGroup("Music");
            }

            // Initialize SFX pool (requires a prefab template)
            var sfxSourceTemplate = new GameObject("[Zero.AudioSfxSource]")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            if (Application.isPlaying)
            {
                Object.DontDestroyOnLoad(sfxSourceTemplate);
            }
            sfxSourceTemplate.SetActive(false);
            sfxSourceTemplate.AddComponent<AudioSource>();
            _sfxPool = _poolService.GetPool(sfxSourceTemplate);

            _log.Info("[AUDIO] Initialized (mixer: " + (_mixer != null ? "loaded" : "not found") + ")");
        }

        public async UniTask PlayMusicAsync(string clipKey, bool loop = true, CancellationToken ct = default)
        {
            if (_musicSource == null)
            {
                _log.Warn("[AUDIO] Music source not initialized");
                return;
            }

            // Fade out current music (0.3s)
            if (_musicSource.isPlaying)
            {
                await LMotion.Create(_musicSource.volume, 0f, 0.3f)
                    .BindToAudioSourceVolume(_musicSource)
                    .ToUniTask(cancellationToken: ct);
            }

            // Load and set new clip
            try
            {
                var clip = await _assetService.LoadAsync<AudioClip>(clipKey, ct);
                _musicSource.clip = clip;
            }
            catch (Exception ex)
            {
                _log.Error($"[AUDIO] Failed to load music '{clipKey}': {ex.Message}");
                return;
            }

            _musicSource.loop = loop;
            _musicSource.Play();

            // Fade in new music (0.3s)
            await LMotion.Create(0f, 1f, 0.3f)
                .BindToAudioSourceVolume(_musicSource)
                .ToUniTask(cancellationToken: ct);
        }

        public void StopMusic()
        {
            if (_musicSource != null)
            {
                _musicSource.Stop();
            }
        }

        public async UniTask PlaySfxAsync(string clipKey, CancellationToken ct = default)
        {
            try
            {
                var clip = await _assetService.LoadAsync<AudioClip>(clipKey, ct);
                var source = _sfxPool.Spawn();

                if (_mixer != null)
                {
                    source.outputAudioMixerGroup = GetMixerGroup("Sfx");
                }

                source.clip = clip;
                source.PlayOneShot(clip);

                // Auto-return to pool after clip ends
                float delaySeconds = clip.length;
                await UniTask.Delay((int)(delaySeconds * 1000), cancellationToken: ct);
                _sfxPool.Despawn(source);
            }
            catch (Exception ex)
            {
                _log.Error($"[AUDIO] Failed to play SFX '{clipKey}': {ex.Message}");
            }
        }

        public void SetBusVolume(AudioBus bus, float volume)
        {
            float clamped = Mathf.Clamp01(volume);
            _busVolumes[bus] = clamped;

            if (_mixer != null)
            {
                string paramName = $"Bus{bus}";
                float db = LinearToDecibel(clamped);
                _mixer.SetFloat(paramName, db);
            }

            // Persist to save service
            string key = $"audio.bus.{bus.ToString().ToLowerInvariant()}";
            _saveService.Set(key, clamped);
        }

        public float GetBusVolume(AudioBus bus)
        {
            return _busVolumes.TryGetValue(bus, out var v) ? v : 1f;
        }

        private AudioMixerGroup GetMixerGroup(string busName)
        {
            if (_mixer == null) return null;
            return _mixer.FindMatchingGroups(busName).Length > 0 ? _mixer.FindMatchingGroups(busName)[0] : null;
        }

        private static float LinearToDecibel(float linear)
        {
            if (linear <= 0) return -80f;
            return Mathf.Log10(linear) * 20f;
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

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_musicSourceGo != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(_musicSourceGo);
                else
                    Object.DestroyImmediate(_musicSourceGo);
            }

            if (_sfxPool != null)
            {
                // Pool cleanup handled by IPoolService
            }
        }
    }
}
