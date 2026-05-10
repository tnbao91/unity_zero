using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;
using UnityEngine.TestTools;
using Zero.Core;
using Zero.Services.VersionCheck;

namespace Zero.Tests.EditMode
{
    public class VersionCheckServiceTests
    {
        private MockRemoteConfigService _remoteConfig;
        private MockLogService _log;
        private VersionCheckService _service;

        [SetUp]
        public void Setup()
        {
            _remoteConfig = new MockRemoteConfigService();
            _log = new MockLogService();
            // Inject a known semver so tests don't depend on Application.version
            // (default template ProductVersion is "0.1" which fails 3-part parse).
            _service = new VersionCheckService(_remoteConfig, _log, "1.0.0");
        }

        [Test]
        public void LastResult_InitiallyOk()
        {
            Assert.AreEqual(VersionStatus.Ok, _service.LastResult.Status);
        }

        [UnityTest]
        public IEnumerator CheckAsync_MaintenanceModeTrue_ReturnsMaintenance() => UniTask.ToCoroutine(async () =>
        {
            _remoteConfig.SetBool("maintenance_mode", true);

            var result = await _service.CheckAsync(CancellationToken.None);

            Assert.AreEqual(VersionStatus.Maintenance, result.Status);
            Assert.AreEqual(VersionStatus.Maintenance, _service.LastResult.Status);
        });

        [UnityTest]
        public IEnumerator CheckAsync_NoMinVersionKey_ReturnsOkWithWarn() => UniTask.ToCoroutine(async () =>
        {
            var result = await _service.CheckAsync(CancellationToken.None);

            Assert.AreEqual(VersionStatus.Ok, result.Status);
            Assert.IsTrue(_log.HasWarnedAbout("min_version"));
        });

        [UnityTest]
        public IEnumerator CheckAsync_LocalVersionLessThanMin_ReturnsForceUpdate() => UniTask.ToCoroutine(async () =>
        {
            _remoteConfig.SetString("min_version", "2.0.0");

            var result = await _service.CheckAsync(CancellationToken.None);

            Assert.AreEqual(VersionStatus.ForceUpdate, result.Status);
            Assert.AreEqual("2.0.0", result.RemoteMinVersion);
        });

        [UnityTest]
        public IEnumerator CheckAsync_LocalVersionGreaterThanMin_ReturnsOk() => UniTask.ToCoroutine(async () =>
        {
            _remoteConfig.SetString("min_version", "0.1.0");

            var result = await _service.CheckAsync(CancellationToken.None);

            Assert.AreEqual(VersionStatus.Ok, result.Status);
        });

        [UnityTest]
        public IEnumerator CheckAsync_LocalVersionBetweenMinAndRecommended_ReturnsSoftUpdate() => UniTask.ToCoroutine(async () =>
        {
            _remoteConfig.SetString("min_version", "0.1.0");
            _remoteConfig.SetString("recommended_version", "2.0.0");

            var result = await _service.CheckAsync(CancellationToken.None);

            Assert.AreEqual(VersionStatus.SoftUpdate, result.Status);
        });

        [UnityTest]
        public IEnumerator CheckAsync_LocalVersionGreaterThanRecommended_ReturnsOk() => UniTask.ToCoroutine(async () =>
        {
            _remoteConfig.SetString("min_version", "0.1.0");
            _remoteConfig.SetString("recommended_version", "0.5.0");

            var result = await _service.CheckAsync(CancellationToken.None);

            Assert.AreEqual(VersionStatus.Ok, result.Status);
        });

        [UnityTest]
        public IEnumerator CheckAsync_MalformedMinVersion_ReturnsOkWithWarn() => UniTask.ToCoroutine(async () =>
        {
            _remoteConfig.SetString("min_version", "invalid");

            var result = await _service.CheckAsync(CancellationToken.None);

            Assert.AreEqual(VersionStatus.Ok, result.Status);
            Assert.IsTrue(_log.HasWarnedAbout("Invalid semver"));
        });

        [UnityTest]
        public IEnumerator CheckAsync_CancellationToken_ThrowsOperationCanceledException() => UniTask.ToCoroutine(async () =>
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(() => _service.CheckAsync(cts.Token).AsTask());
            await UniTask.CompletedTask;
        });

        private sealed class MockRemoteConfigService : IRemoteConfigService
        {
            private readonly Dictionary<string, object> _values = new();

            public Observable<Unit> OnConfigUpdated => Observable.Empty<Unit>();

            public void SetString(string key, string value) => _values[key] = value;
            public void SetBool(string key, bool value) => _values[key] = value;
            public void SetLong(string key, long value) => _values[key] = value;
            public void SetDouble(string key, double value) => _values[key] = value;

            public UniTask<bool> FetchAndActivateAsync(System.TimeSpan timeout, CancellationToken ct = default) => UniTask.FromResult(true);

            public T GetVariant<T>(string key, T defaultValue) => defaultValue;

            public bool TryGetString(string key, out string value)
            {
                if (_values.TryGetValue(key, out var obj) && obj is string s)
                {
                    value = s;
                    return true;
                }
                value = null;
                return false;
            }

            public bool TryGetLong(string key, out long value)
            {
                if (_values.TryGetValue(key, out var obj) && obj is long l)
                {
                    value = l;
                    return true;
                }
                value = 0;
                return false;
            }

            public bool TryGetDouble(string key, out double value)
            {
                if (_values.TryGetValue(key, out var obj) && obj is double d)
                {
                    value = d;
                    return true;
                }
                value = 0;
                return false;
            }

            public bool TryGetBool(string key, out bool value)
            {
                if (_values.TryGetValue(key, out var obj) && obj is bool b)
                {
                    value = b;
                    return true;
                }
                value = false;
                return false;
            }
        }

        private sealed class MockLogService : ILogService
        {
            private readonly List<string> _warns = new();

            public bool IsEnabled { get; set; } = true;

            public void Info(string message) { }
            public void Warn(string message) => _warns.Add(message);
            public void Error(string message) { }
            public void Error(System.Exception exception, string context = null) { }

            public bool HasWarnedAbout(string text) => _warns.Any(w => w.Contains(text));
        }
    }
}
