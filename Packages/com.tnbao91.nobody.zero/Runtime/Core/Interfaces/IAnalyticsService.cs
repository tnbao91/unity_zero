using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Zero.Core
{
    public interface IAnalyticsService
    {
        UniTask InitializeAsync(CancellationToken ct = default);
        void LogEvent(string eventName);
        void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters);
        void SetUserProperty(string name, string value);
        void SetUserId(string userId);
    }
}
