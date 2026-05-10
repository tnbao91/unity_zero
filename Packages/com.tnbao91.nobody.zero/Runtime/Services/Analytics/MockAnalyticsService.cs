using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zero.Core;

namespace Zero.Services.Analytics
{
    public sealed class MockAnalyticsService : IAnalyticsService
    {
        private readonly ILogService _log;
        private readonly StringBuilder _buf = new(128);

        public MockAnalyticsService(ILogService log)
        {
            _log = log;
        }

        public UniTask InitializeAsync(CancellationToken ct = default)
        {
            _log.Info("[ANALYTICS:mock] Initialized");
            return UniTask.CompletedTask;
        }

        public void LogEvent(string eventName)
        {
            _log.Info($"[ANALYTICS:mock] {eventName}");
        }

        public void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters)
        {
            _buf.Clear();
            _buf.Append("[ANALYTICS:mock] ").Append(eventName);
            if (parameters != null && parameters.Count > 0)
            {
                _buf.Append(" {");
                var first = true;
                foreach (var kv in parameters)
                {
                    if (!first) _buf.Append(", ");
                    _buf.Append(kv.Key).Append('=').Append(kv.Value);
                    first = false;
                }
                _buf.Append('}');
            }
            _log.Info(_buf.ToString());
        }

        public void SetUserProperty(string name, string value)
        {
            _log.Info($"[ANALYTICS:mock] property {name}={value}");
        }

        public void SetUserId(string userId)
        {
            _log.Info($"[ANALYTICS:mock] userId={userId}");
        }
    }
}
