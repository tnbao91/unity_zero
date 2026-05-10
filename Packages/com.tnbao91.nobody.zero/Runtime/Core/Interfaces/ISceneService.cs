using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace Zero.Core
{
    public interface ISceneService
    {
        string ActiveScene { get; }
        UniTask LoadAsync(string sceneKey, IProgress<float> progress = null, CancellationToken ct = default);
        UniTask UnloadAsync(string sceneKey, CancellationToken ct = default);
        Observable<string> OnSceneLoaded { get; }
        Observable<string> OnSceneUnloaded { get; }
    }
}
