using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zero.Core;

namespace Zero.Services.Time
{
    public sealed class StubTimeService : ITimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public long UnixTimeSeconds => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public bool IsServerSynced => false;

        public UniTask SyncAsync(CancellationToken ct = default)
        {
            // v1 stub — pure local clock. v2 should sync against backend or NTP.
            return UniTask.CompletedTask;
        }
    }
}
