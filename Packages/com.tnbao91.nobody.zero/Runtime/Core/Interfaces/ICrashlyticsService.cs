using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Zero.Core
{
    public interface ICrashlyticsService
    {
        UniTask InitializeAsync(CancellationToken ct = default);
        void RecordException(Exception exception);
        void Log(string message);
        void SetCustomKey(string key, string value);
        void SetUserId(string userId);
    }
}
