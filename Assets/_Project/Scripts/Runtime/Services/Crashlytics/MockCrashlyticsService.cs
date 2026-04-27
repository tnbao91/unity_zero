using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zero.Core;

namespace Zero.Services.Crashlytics
{
    public sealed class MockCrashlyticsService : ICrashlyticsService
    {
        private readonly ILogService _log;

        public MockCrashlyticsService(ILogService log)
        {
            _log = log;
        }

        public UniTask InitializeAsync(CancellationToken ct = default)
        {
            _log.Info("[CRASHLYTICS:mock] Initialized");
            return UniTask.CompletedTask;
        }

        public void RecordException(Exception exception)
        {
            _log.Info($"[CRASHLYTICS:mock] Exception: {exception.GetType().Name}: {exception.Message}");
        }

        public void Log(string message)
        {
            _log.Info($"[CRASHLYTICS:mock] Log: {message}");
        }

        public void SetCustomKey(string key, string value)
        {
            _log.Info($"[CRASHLYTICS:mock] Key {key}={value}");
        }

        public void SetUserId(string userId)
        {
            _log.Info($"[CRASHLYTICS:mock] UserId={userId}");
        }
    }
}
