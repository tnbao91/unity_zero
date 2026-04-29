using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zero.Core;

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

        private readonly IAssetService _assetService;
        private readonly Transform _toastLayer;
        private readonly Queue<string> _queue = new();
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

        public void Show(string text, float durationSeconds = 2f)
        {
            if (_queue.Count >= MaxQueued)
            {
                Debug.LogWarning($"[UI] Toast queue at max capacity ({MaxQueued}). Dropping oldest message.");
                _queue.Dequeue();
            }

            _queue.Enqueue(text);

            if (!_isShowingToast)
            {
                _ = ProcessQueueAsync();
            }
        }

        private async UniTask ProcessQueueAsync()
        {
            while (_queue.Count > 0 && !_disposed)
            {
                string text = _queue.Dequeue();
                _isShowingToast = true;

                // In a real implementation, this would instantiate the toast prefab,
                // set the text, and wait for the duration. For now, this is a placeholder.
                // The actual rendering is deferred to consumer who provides the toast prefab.
                await UniTask.Delay(2000);

                _isShowingToast = false;
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
