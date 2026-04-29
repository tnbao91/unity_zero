using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zero.Core;
using Zero.Core.Events;

namespace Zero.UI
{
    /// <summary>
    /// Main UI service managing popups, screens, and toasts.
    /// Popups are managed via a stack with layer-aware sort orders.
    /// Screens are fullscreen and replace each other (no stack).
    /// Toasts are a FIFO queue.
    /// </summary>
    public sealed class UIService : IUIService, IDisposable
    {
        private readonly IAssetService _assetService;
        private readonly IEventBus _eventBus;
        private readonly ILogService _logService;
        private readonly Dictionary<Core.UiLayer, Transform> _layerRoots = new();
        private readonly Dictionary<Core.UiLayer, PopupStack> _popupStacks = new();
        private readonly Dictionary<string, IAssetHandle> _loadedHandles = new();

        private ScreenManager _screenManager;
        private ToastQueue _toastQueue;
        private bool _disposed;

        public UIService(IAssetService assetService, IEventBus eventBus, ILogService logService)
        {
            _assetService = assetService;
            _eventBus = eventBus;
            _logService = logService;
        }

        public async UniTask InitializeAsync(CancellationToken ct = default)
        {
            // Build layer canvases
            _layerRoots = LayerCanvas.Build();

            // Initialize popup stacks for each layer
            foreach (Core.UiLayer layer in System.Enum.GetValues(typeof(Core.UiLayer)))
            {
                _popupStacks[layer] = new PopupStack();
            }

            // Initialize managers
            _screenManager = new ScreenManager(_assetService, _layerRoots[Core.UiLayer.Popup]);
            _toastQueue = new ToastQueue(_assetService, _layerRoots[Core.UiLayer.System]);

            await _toastQueue.InitializeAsync(ct);

            _logService.Info("[UI] Initialized (4 layer canvases spawned)");
        }

        public async UniTask<TResult> PushAsync<TPopup, TData, TResult>(
            TData data,
            PopupTransition transition = PopupTransition.Fade,
            float duration = 0.2f,
            CancellationToken ct = default)
            where TPopup : class
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UIService));

            var popupType = typeof(TPopup);
            string popupKey = popupType.Name.ToLowerInvariant();

            // Load popup prefab via Addressables
            string prefabKey = $"ui/popup/{popupKey}";
            IAssetHandle<GameObject> prefabHandle = null;

            try
            {
                prefabHandle = await _assetService.LoadAsync<GameObject>(prefabKey, ct);
                _loadedHandles[popupKey] = prefabHandle;

                // Instantiate the popup
                GameObject popupInstance = Object.Instantiate(
                    prefabHandle.Asset,
                    _layerRoots[Core.UiLayer.Popup],
                    worldPositionStays: false);

                // Get the PopupBase component
                var popupComponent = popupInstance.GetComponent<PopupBase<TData, TResult>>();
                if (popupComponent == null)
                {
                    Debug.LogError($"[UI] Popup '{popupKey}' does not have PopupBase<{typeof(TData).Name}, {typeof(TResult).Name}> component", popupInstance);
                    Object.Destroy(popupInstance);
                    throw new InvalidOperationException($"Popup {popupKey} missing PopupBase component");
                }

                // Create and wire the handle
                var handle = new PopupHandle<TResult>();
                popupComponent.SetHandle(handle);

                // Set the sort order
                var canvas = popupInstance.GetComponent<Canvas>();
                if (canvas != null)
                {
                    int sortOrder = _popupStacks[Core.UiLayer.Popup].Push(popupInstance, (int)Core.UiLayer.Popup);
                    canvas.sortingOrder = sortOrder;
                }

                // Call OnOpenAsync
                try
                {
                    await popupComponent.OnOpenAsync(data, ct);
                }
                catch (OperationCanceledException)
                {
                    Object.Destroy(popupInstance);
                    throw;
                }

                // Publish event
                _eventBus.Publish(new PopupOpened(popupKey));

                // Wait for the result
                var result = await handle.Result;

                // Close the popup
                await popupComponent.OnCloseAsync(result, ct);
                Object.Destroy(popupInstance);

                // Pop from stack
                _popupStacks[Core.UiLayer.Popup].TryPop(out _);

                // Publish close event
                _eventBus.Publish(new PopupClosed(popupKey));

                return result;
            }
            catch (Exception ex)
            {
                _logService.Error($"[UI] Failed to push popup '{popupKey}': {ex.Message}");
                throw;
            }
            finally
            {
                // Don't dispose the handle yet — keep it alive for potential re-use
            }
        }

        public async UniTask PopAsync(CancellationToken ct = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UIService));

            if (_popupStacks[Core.UiLayer.Popup].TryPop(out var popupInstance))
            {
                Object.Destroy(popupInstance);
                await UniTask.Yield(cancellationToken: ct);
            }
        }

        public async UniTask ReplaceAsync<TPopup, TData, TResult>(
            TData data,
            PopupTransition transition = PopupTransition.Fade,
            float duration = 0.2f,
            CancellationToken ct = default)
            where TPopup : class
        {
            // Pop the current popup
            await PopAsync(ct);

            // Push the new one
            _ = await PushAsync<TPopup, TData, TResult>(data, transition, duration, ct);
        }

        public async UniTask ShowScreenAsync<TScreen, TData>(TData data, CancellationToken ct = default)
            where TScreen : class
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UIService));

            await _screenManager.ShowAsync<TScreen, TData>(data, ct);
        }

        public void ShowToast(string text, float duration = 2f)
        {
            if (_disposed)
                return;

            _toastQueue.Show(text, duration);
        }

        public Transform GetLayerRoot(Core.UiLayer layer)
        {
            if (_layerRoots.TryGetValue(layer, out var root))
            {
                return root;
            }

            return null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _screenManager?.Dispose();

            _toastQueue?.Dispose();

            // Dispose all loaded handles
            foreach (var handle in _loadedHandles.Values)
            {
                (handle as IDisposable)?.Dispose();
            }
            _loadedHandles.Clear();

            // Clear popup stacks
            foreach (var stack in _popupStacks.Values)
            {
                stack.Clear();
            }
            _popupStacks.Clear();
        }
    }
}
