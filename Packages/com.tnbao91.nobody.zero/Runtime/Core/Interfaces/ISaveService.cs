using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace Zero.Core
{
    public interface ISaveService
    {
        UniTask LoadAsync(CancellationToken ct = default);
        UniTask SaveAsync(CancellationToken ct = default);
        void RequestSave();
        bool TryGet<T>(string key, out T value);
        void Set<T>(string key, T value);
        void Delete(string key);
        Observable<Unit> OnLoaded { get; }
    }
}
