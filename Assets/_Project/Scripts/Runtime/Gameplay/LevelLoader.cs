using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zero.Core;

namespace Zero.Gameplay
{
    /// <summary>
    /// Loads a level prefab via IAssetService + Addressables key.
    /// Returns the instantiated GameObject + a handle for later release.
    /// </summary>
    public sealed class LevelLoader
    {
        private readonly IAssetService _assetService;

        public LevelLoader(IAssetService assetService)
        {
            _assetService = assetService ?? throw new ArgumentNullException(nameof(assetService));
        }

        /// <summary>
        /// Loads a level prefab from Addressables and instantiates it.
        /// Caller is responsible for Dispose()ing the returned handle to release the prefab.
        /// </summary>
        public async UniTask<(GameObject Instance, IAssetHandle<GameObject> Handle)> LoadLevelAsync(
            string addressableKey,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(addressableKey))
                throw new ArgumentException("Addressable key cannot be empty.", nameof(addressableKey));

            var handle = await _assetService.LoadAsync<GameObject>(addressableKey, ct);
            var instance = Object.Instantiate(handle.Asset);
            return (instance, handle);
        }
    }
}
