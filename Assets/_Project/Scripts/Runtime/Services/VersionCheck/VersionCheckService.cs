using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zero.Core;

namespace Zero.Services.VersionCheck
{
    public sealed class VersionCheckService : IVersionCheckService
    {
        private const string MinVersionKey = "min_version";
        private const string RecommendedVersionKey = "recommended_version";
        private const string MaintenanceModeKey = "maintenance_mode";

        private readonly IRemoteConfigService _remoteConfig;
        private readonly ILogService _log;

        private VersionCheckResult _lastResult;

        public VersionCheckResult LastResult => _lastResult;

        public VersionCheckService(IRemoteConfigService remoteConfig, ILogService log)
        {
            _remoteConfig = remoteConfig;
            _log = log;
            _lastResult = new VersionCheckResult(VersionStatus.Ok, Application.version, "");
        }

        public UniTask<VersionCheckResult> CheckAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            // Check maintenance mode first
            if (_remoteConfig.TryGetBool(MaintenanceModeKey, out var maintenance) && maintenance)
            {
                _lastResult = new VersionCheckResult(VersionStatus.Maintenance, Application.version, "");
                return UniTask.FromResult(_lastResult);
            }

            // Get remote min version
            if (!_remoteConfig.TryGetString(MinVersionKey, out var remoteMinVersion))
            {
                _log.Warn($"[VersionCheck] Missing '{MinVersionKey}' in remote config; assuming OK");
                _lastResult = new VersionCheckResult(VersionStatus.Ok, Application.version, "");
                return UniTask.FromResult(_lastResult);
            }

            var localVersion = Application.version;
            var minVersionParsed = ParseVersion(remoteMinVersion);
            var localVersionParsed = ParseVersion(localVersion);

            // Check if local is below minimum
            if (!minVersionParsed.IsValid || !localVersionParsed.IsValid)
            {
                _log.Warn($"[VersionCheck] Invalid semver format (local={localVersion}, min={remoteMinVersion}); assuming OK");
                _lastResult = new VersionCheckResult(VersionStatus.Ok, localVersion, remoteMinVersion);
                return UniTask.FromResult(_lastResult);
            }

            if (CompareVersions(localVersionParsed, minVersionParsed) < 0)
            {
                _lastResult = new VersionCheckResult(VersionStatus.ForceUpdate, localVersion, remoteMinVersion);
                return UniTask.FromResult(_lastResult);
            }

            // Check if local is below recommended (soft update)
            if (_remoteConfig.TryGetString(RecommendedVersionKey, out var remoteRecommendedVersion))
            {
                var recommendedVersionParsed = ParseVersion(remoteRecommendedVersion);
                if (recommendedVersionParsed.IsValid && CompareVersions(localVersionParsed, recommendedVersionParsed) < 0)
                {
                    _lastResult = new VersionCheckResult(VersionStatus.SoftUpdate, localVersion, remoteMinVersion);
                    return UniTask.FromResult(_lastResult);
                }
            }

            _lastResult = new VersionCheckResult(VersionStatus.Ok, localVersion, remoteMinVersion);
            return UniTask.FromResult(_lastResult);
        }

        private static ParsedVersion ParseVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return new ParsedVersion(false, 0, 0, 0);

            var parts = version.Split('.');
            if (parts.Length < 3)
                return new ParsedVersion(false, 0, 0, 0);

            if (!int.TryParse(parts[0], out var major) ||
                !int.TryParse(parts[1], out var minor) ||
                !int.TryParse(parts[2], out var patch))
            {
                return new ParsedVersion(false, 0, 0, 0);
            }

            return new ParsedVersion(true, major, minor, patch);
        }

        /// <summary>
        /// Compare two parsed versions. Returns:
        /// negative if a < b
        /// zero if a == b
        /// positive if a > b
        /// </summary>
        private static int CompareVersions(ParsedVersion a, ParsedVersion b)
        {
            if (a.Major != b.Major)
                return a.Major.CompareTo(b.Major);
            if (a.Minor != b.Minor)
                return a.Minor.CompareTo(b.Minor);
            return a.Patch.CompareTo(b.Patch);
        }

        private readonly struct ParsedVersion
        {
            public bool IsValid { get; }
            public int Major { get; }
            public int Minor { get; }
            public int Patch { get; }

            public ParsedVersion(bool isValid, int major, int minor, int patch)
            {
                IsValid = isValid;
                Major = major;
                Minor = minor;
                Patch = patch;
            }
        }
    }
}
