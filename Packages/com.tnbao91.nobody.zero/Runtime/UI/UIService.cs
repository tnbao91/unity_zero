using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Zero.Core;
using Zero.Core.Events;
using Object = UnityEngine.Object;

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
        private readonly Stack<(GameObject instance, IPopupHandle handle, string key, GameObject backdrop)> _activePopups = new();

        private ScreenManager _screenManager;
        private ToastQueue _toastQueue;
        private bool _disposed;
        private bool _rootAttached;

        public UIService(IAssetService assetService, IEventBus eventBus, ILogService logService)
        {
            _assetService = assetService;
            _eventBus = eventBus;
            _logService = logService;
        }

        public void AttachRoot(IReadOnlyDictionary<Core.UiLayer, Transform> layers)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UIService));
            if (layers == null)
                throw new ArgumentNullException(nameof(layers));
            if (_rootAttached)
            {
                _logService.Warn("[UI] AttachRoot called while a root is already attached. Detaching first.");
                DetachRoot();
            }

            _layerRoots.Clear();
            foreach (var kv in layers)
            {
                if (kv.Value == null)
                {
                    _logService.Warn($"[UI] AttachRoot: layer '{kv.Key}' has no Transform; skipping.");
                    continue;
                }
                _layerRoots[kv.Key] = kv.Value;
            }

            _popupStacks.Clear();
            foreach (Core.UiLayer layer in Enum.GetValues(typeof(Core.UiLayer)))
            {
                _popupStacks[layer] = new PopupStack();
            }

            if (_layerRoots.TryGetValue(Core.UiLayer.Hud, out var hudRoot))
                _screenManager = new ScreenManager(_assetService, hudRoot);
            if (_layerRoots.TryGetValue(Core.UiLayer.System, out var sysRoot))
            {
                _toastQueue = new ToastQueue(_assetService, sysRoot);
                _toastQueue.InitializeAsync().Forget();
            }

            _rootAttached = true;
            _logService.Info("[UI] Root attached.");
        }

        public void DetachRoot()
        {
            if (!_rootAttached) return;

            _screenManager?.Dispose();
            _screenManager = null;
            _toastQueue?.Dispose();
            _toastQueue = null;

            _activePopups.Clear();
            foreach (var stack in _popupStacks.Values) stack.Clear();
            _popupStacks.Clear();
            _layerRoots.Clear();

            _rootAttached = false;
            _logService.Info("[UI] Root detached.");
        }

        private void EnsureRootAttached()
        {
            if (!_rootAttached)
                throw new InvalidOperationException(
                    "UIService has no UIRoot attached. Add a UIRoot MonoBehaviour to the active scene and assign its layer Transforms.");
        }

        private GameObject CreateBackdrop(string popupKey, int sortOrder)
        {
            var backdropGo = new GameObject($"[Backdrop:{popupKey}]");
            backdropGo.transform.SetParent(_layerRoots[Core.UiLayer.Popup], false);

            var rectTransform = backdropGo.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            var canvas = backdropGo.AddComponent<Canvas>();
            canvas.sortingOrder = sortOrder - 1; // Render behind the popup

            // GraphicRaycaster needed for UGUI raycasting on override-sorting sub-canvas
            backdropGo.AddComponent<GraphicRaycaster>();

            var image = backdropGo.AddComponent<Image>();
            image.color = new Color(0, 0, 0, 0.5f); // Semi-transparent black
            image.raycastTarget = true;

            var backdropHandler = backdropGo.AddComponent<BackdropTapHandler>();
            backdropHandler.Initialize(popupKey, _eventBus);

            return backdropGo;
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
            EnsureRootAttached();

            var popupType = typeof(TPopup);
            string popupKey = popupType.Name.ToLowerInvariant();

            // Load popup prefab via Addressables
            string prefabKey = $"ui/popup/{popupKey}";
            IAssetHandle<GameObject> prefabHandle = null;

            try
            {
                prefabHandle = await _assetService.LoadAsync<GameObject>(prefabKey, ct);

                // Create backdrop (modal mask)
                // Use PeekNextSortOrder to avoid duplicating the PopupStack formula
                int popupSortOrder = _popupStacks[Core.UiLayer.Popup].PeekNextSortOrder((int)Core.UiLayer.Popup);
                GameObject backdrop = CreateBackdrop(popupKey, popupSortOrder);

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
                    UiObjects.SafeDestroy(popupInstance);
                    throw new InvalidOperationException($"Popup {popupKey} missing PopupBase component");
                }

                // Create and wire the handle
                var handle = new PopupHandle<TResult>();
                popupComponent.SetHandle(handle);

                // Track the active popup for potential cancellation (includes backdrop)
                _activePopups.Push((popupInstance, (IPopupHandle)handle, popupKey, backdrop));

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
                    UiObjects.SafeDestroy(popupInstance);
                    if (backdrop != null) UiObjects.SafeDestroy(backdrop);
                    if (_activePopups.Count > 0) _activePopups.Pop();
                    _popupStacks[Core.UiLayer.Popup].TryPop(out _);
                    throw;
                }

                // Publish event
                _eventBus.Publish(new PopupOpened(popupKey));

                // Wait for the result
                TResult result = default;
                try
                {
                    result = await handle.Result;
                }
                catch (OperationCanceledException)
                {
                    // Handle was cancelled externally (e.g., by PopAsync).
                    // PopupClosed is published by PopAsync before calling Cancel(); we don't republish here.
                    throw;
                }

                // Close the popup
                await popupComponent.OnCloseAsync(result, ct);
                UiObjects.SafeDestroy(popupInstance);
                if (backdrop != null) UiObjects.SafeDestroy(backdrop);

                // Pop from stack
                _popupStacks[Core.UiLayer.Popup].TryPop(out _);

                // Remove from active popups
                if (_activePopups.Count > 0 && _activePopups.Peek().backdrop == backdrop)
                {
                    _activePopups.Pop();
                }

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
                // Dispose prefab handle immediately after instantiation is done
                prefabHandle?.Dispose();
            }
        }

        public async UniTask PopAsync(CancellationToken ct = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UIService));
            EnsureRootAttached();

            if (_activePopups.Count > 0)
            {
                var (popupInstance, handle, popupKey, backdrop) = _activePopups.Pop();

                // Cancel the handle to unblock the PushAsync awaiter
                handle.Cancel();

                // Destroy the popup and its backdrop
                UiObjects.SafeDestroy(popupInstance);
                if (backdrop != null) UiObjects.SafeDestroy(backdrop);

                // Pop from stack
                _popupStacks[Core.UiLayer.Popup].TryPop(out _);

                // Publish close event
                _eventBus.Publish(new PopupClosed(popupKey));

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
            EnsureRootAttached();

            await _screenManager.ShowAsync<TScreen, TData>(data, ct);
        }

        public void ShowToast(string text, float duration = 2f)
        {
            if (_disposed) return;
            if (!_rootAttached)
            {
                _logService.Warn("[UI] ShowToast called with no UIRoot attached. Message dropped: " + text);
                return;
            }

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

            // Clear active popups
            _activePopups.Clear();

            // Clear popup stacks
            foreach (var stack in _popupStacks.Values)
            {
                stack.Clear();
            }
            _popupStacks.Clear();
        }
    }
}
