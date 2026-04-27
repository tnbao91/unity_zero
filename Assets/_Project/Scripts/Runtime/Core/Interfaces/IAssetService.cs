using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Zero.Core
{
    public interface IAssetHandle<T> : IDisposable where T : UnityEngine.Object
    {
        T Asset { get; }
        bool IsLoaded { get; }
    }

    public interface IAssetService
    {
        int ActiveHandleCount { get; }
        UniTask InitializeAsync(CancellationToken ct = default);
        UniTask<IAssetHandle<T>> LoadAsync<T>(string key, CancellationToken ct = default) where T : UnityEngine.Object;
        UniTask PreloadAsync(IReadOnlyList<string> keys, IProgress<float> progress = null, CancellationToken ct = default);
    }
}
