using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Zero.Core
{
    public interface ITimeService
    {
        DateTime UtcNow { get; }
        long UnixTimeSeconds { get; }
        bool IsServerSynced { get; }
        UniTask SyncAsync(CancellationToken ct = default);
    }
}
