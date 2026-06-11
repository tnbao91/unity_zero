using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Zero.Core;
using Zero.Infrastructure;
using Object = UnityEngine.Object;

namespace Zero.UI
{
    /// <summary>
    /// FIFO queue for displaying short toast messages.
    /// Automatically manages display order and duration.
    /// Toast prefab loaded from Addressables key "ui/toast/default".
    /// </summary>
    internal sealed class ToastQueue : IDisposable
    {
        private const int MaxQueued = 16;
        private const string ToastPrefabKey = "ui/toast/default";
        internal const float MinDurationSeconds = 0.5f;

        private readonly IAssetService _assetService;
        private readonly Transform _toastLayer;
        private readonly Queue<(string text, float duration)> _queue = new();
        private IAssetHandle<GameObject> _toastPrefabHandle;
        private bool _isShowingToast;
        private bool _disposed;

        public ToastQueue(IAssetService assetService, Transform toastLayer)
        {
            _assetService = assetService;
            _toastLayer = toastLayer;
        }

        public async UniTask InitializeAsync(CancellationToken ct = default)
        {
            // Pre-check for optional toast prefab key
            if (await _assetService.HasKeyAsync<GameObject>(ToastPrefabKey, ct))
            {
                try
                {
                    _toastPrefabHandle = await _assetService.LoadAsync<GameObject>(ToastPrefabKey, ct);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[UI] Failed to load toast prefab from '{ToastPrefabKey}': {ex.Message}. Toast functionality disabled.");
                    _toastPrefabHandle = null;
                }
            }
            else
            {
                Debug.LogWarning($"[UI] Toast prefab key '{ToastPrefabKey}' not found. Toast functionality disabled.");
            }
        }

        // A zero/negative duration would flash the toast for a single frame —
        // always a caller bug, so fail safe by clamping instead of throwing.
        internal static float ClampDuration(float durationSeconds)
            => durationSeconds < MinDurationSeconds ? MinDurationSeconds : durationSeconds;

        public void Show(string text, float durationSeconds = 2f)
        {
            if (_disposed) return;
            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.LogWarning("[UI] Toast with empty text ignored.");
                return;
            }
            durationSeconds = ClampDuration(durationSeconds);

            if (_queue.Count >= MaxQueued)
            {
                Debug.LogWarning($"[UI] Toast queue at max capacity ({MaxQueued}). Dropping oldest message.");
                _queue.Dequeue();
            }

            _queue.Enqueue((text, durationSeconds));

            if (!_isShowingToast)
            {
                _ = ProcessQueueAsync();
            }
        }

        private async UniTask ProcessQueueAsync()
        {
            while (_queue.Count > 0 && !_disposed)
            {
                var (text, durationSeconds) = _queue.Dequeue();
                _isShowingToast = true;

                GameObject toastInstance = null;
                try
                {
                    // If toast prefab is available, instantiate and display it
                    if (_toastPrefabHandle != null)
                    {
                        toastInstance = Object.Instantiate(
                            _toastPrefabHandle.Asset,
                            _toastLayer,
                            worldPositionStays: false);

                        // Find and set the text component
                        var textComponent = toastInstance.GetComponentInChildren<TextMeshProUGUI>();
                        if (textComponent != null)
                        {
                            textComponent.text = text;
                        }
                        else
                        {
                            Debug.LogWarning($"[UI] Toast prefab does not have a TextMeshProUGUI component. Text will not be displayed.");
                        }
                    }
                    else
                    {
                        Debug.Log($"[UI] Toast prefab not available. Message: {text}");
                    }

                    // Wait for the duration. UniTask.Delay never resumes in EditMode
                    // (no player loop) — guard so the queue loop doesn't hang in tests.
                    if (Application.isPlaying)
                        await UniTask.Delay(TimeSpan.FromSeconds(durationSeconds));

                    // Destroy the toast instance
                    if (toastInstance != null)
                    {
                        Util.SafeDestroy(toastInstance);
                    }
                }
                finally
                {
                    _isShowingToast = false;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _toastPrefabHandle?.Dispose();
            _queue.Clear();
        }
    }
}
