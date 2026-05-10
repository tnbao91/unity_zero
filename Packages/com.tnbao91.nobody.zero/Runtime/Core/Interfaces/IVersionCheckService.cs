using System.Threading;
using Cysharp.Threading.Tasks;

namespace Zero.Core
{
    public enum VersionStatus
    {
        Ok,
        SoftUpdate,
        ForceUpdate,
        Maintenance
    }

    public readonly struct VersionCheckResult
    {
        public VersionStatus Status { get; }
        public string LocalVersion { get; }
        public string RemoteMinVersion { get; }

        public VersionCheckResult(VersionStatus status, string localVersion, string remoteMinVersion)
        {
            Status = status;
            LocalVersion = localVersion;
            RemoteMinVersion = remoteMinVersion;
        }
    }

    public interface IVersionCheckService
    {
        UniTask<VersionCheckResult> CheckAsync(CancellationToken ct = default);
        VersionCheckResult LastResult { get; }
    }
}
