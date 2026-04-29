using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zero.Core;

namespace Zero.UI
{
    /// <summary>
    /// Manages fullscreen screen replacement (one-at-a-time, no stack).
    /// Screens are loaded via Addressables and displayed in the Popup layer.
    /// Only one screen is active at a time.
    /// </summary>
    internal sealed class ScreenManager : IDisposable
    {
        private readonly IAssetService _assetService;
        private readonly Transform _screenLayer;
        private GameObject _currentScreenInstance;
        private IAssetHandle<GameObject> _currentScreenHandle;
        private bool _disposed;

        public ScreenManager(IAssetService assetService, Transform screenLayer)
        {
            _assetService = assetService;
            _screenLayer = screenLayer;
        }

        public async UniTask ShowAsync<TScreen, TData>(TData data, CancellationToken ct = default)
            where TScreen : class
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ScreenManager));
            }

            // Unload previous screen
            await UnloadScreenAsync();

            // Load new screen prefab
            string screenKey = $"ui/screen/{typeof(TScreen).Name.ToLowerInvariant()}";
            _currentScreenHandle = await _assetService.LoadAsync<GameObject>(screenKey, ct);

            // Instantiate and parent
            _currentScreenInstance = Object.Instantiate(
                _currentScreenHandle.Asset,
                _screenLayer,
                worldPositionStays: false);

            // Allow subclass to initialize if it implements IScreen<T>
            if (_currentScreenInstance.TryGetComponent<IScreen<TData>>(out var screenInit))
            {
                await screenInit.InitializeAsync(data, ct);
            }
        }

        private async UniTask UnloadScreenAsync()
        {
            if (_currentScreenInstance != null)
            {
                Object.Destroy(_currentScreenInstance);
                _currentScreenInstance = null;
            }

            if (_currentScreenHandle != null)
            {
                _currentScreenHandle.Dispose();
                _currentScreenHandle = null;
            }

            await UniTask.Yield();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_currentScreenInstance != null)
            {
                Object.Destroy(_currentScreenInstance);
            }

            _currentScreenHandle?.Dispose();
        }
    }

    /// <summary>
    /// Optional interface for screens that need custom initialization logic.
    /// Screens can implement this interface to receive data during ShowScreenAsync.
    /// </summary>
    public interface IScreen<in TData>
    {
        UniTask InitializeAsync(TData data, CancellationToken ct = default);
    }
}
