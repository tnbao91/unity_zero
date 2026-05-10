using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Zero.Core
{
    public interface INotificationService
    {
        UniTask InitializeAsync(CancellationToken ct = default);
        UniTask<bool> RequestPermissionAsync(CancellationToken ct = default);
        void Schedule(string id, string title, string body, TimeSpan delay);
        void Cancel(string id);
        void CancelAll();
    }
}
