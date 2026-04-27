using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Zero.Core
{
    public interface IBootstrapStep
    {
        string Name { get; }
        bool IsCritical { get; }

        // Per-step deadline; pipeline cancels with linked CTS if breached.
        // Network-bound steps (RemoteConfig, Crashlytics) override to widen.
        TimeSpan Timeout { get; }

        // How many extra attempts after first failure for non-critical steps.
        // Critical steps fail-fast on first throw and ignore MaxRetries.
        int MaxRetries { get; }

        UniTask ExecuteAsync(IProgress<float> progress, CancellationToken ct);
    }
}
