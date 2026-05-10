using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zero.Core;

namespace Zero.Services.Notification
{
    public sealed class MockNotificationService : INotificationService
    {
        private readonly ILogService _log;

        public MockNotificationService(ILogService log)
        {
            _log = log;
        }

        public UniTask InitializeAsync(CancellationToken ct = default)
        {
            _log.Info("[NOTIF:mock] Initialized");
            return UniTask.CompletedTask;
        }

        public UniTask<bool> RequestPermissionAsync(CancellationToken ct = default)
        {
            _log.Info("[NOTIF:mock] Permission requested -> granted");
            return UniTask.FromResult(true);
        }

        public void Schedule(string id, string title, string body, TimeSpan delay)
        {
            _log.Info($"[NOTIF:mock] Schedule '{id}' in {delay.TotalSeconds:F0}s: {title}");
        }

        public void Cancel(string id)
        {
            _log.Info($"[NOTIF:mock] Cancel '{id}'");
        }

        public void CancelAll()
        {
            _log.Info("[NOTIF:mock] Cancel all");
        }
    }
}
