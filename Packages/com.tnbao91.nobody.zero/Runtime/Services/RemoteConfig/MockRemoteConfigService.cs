using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using Zero.Core;

namespace Zero.Services.RemoteConfig
{
    public sealed class MockRemoteConfigService : IRemoteConfigService
    {
        private readonly ILogService _log;
        private readonly Dictionary<string, object> _values = new();
        private readonly Subject<Unit> _updated = new();

        public Observable<Unit> OnConfigUpdated => _updated;

        public MockRemoteConfigService(ILogService log)
        {
            _log = log;
            _values["welcome_text"] = "Hello from mock RC";
            _values["max_levels"] = 50L;
            _values["soft_currency_start"] = 100L;
            _values["ad_interstitial_cooldown_sec"] = 30.0;
            _values["att_defer_to_post_onboarding"] = false;
        }

        public async UniTask<bool> FetchAndActivateAsync(TimeSpan timeout, CancellationToken ct = default)
        {
            try
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
                linked.CancelAfter(timeout);
                await UniTask.Delay(200, cancellationToken: linked.Token);
                _log.Info("[REMOTECONFIG:mock] Fetched fresh values");
                _updated.OnNext(Unit.Default);
                return true;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _log.Warn("[REMOTECONFIG:mock] Fetch timed out, using cached values");
                _updated.OnNext(Unit.Default);
                return false;
            }
        }

        public T GetVariant<T>(string key, T defaultValue)
        {
            if (_values.TryGetValue(key, out var raw) && raw is T typed)
            {
                return typed;
            }
            return defaultValue;
        }

        public bool TryGetString(string key, out string value)
        {
            if (_values.TryGetValue(key, out var raw) && raw is string s)
            {
                value = s;
                return true;
            }
            value = null;
            return false;
        }

        public bool TryGetLong(string key, out long value)
        {
            if (_values.TryGetValue(key, out var raw) && raw is long l)
            {
                value = l;
                return true;
            }
            value = 0;
            return false;
        }

        public bool TryGetDouble(string key, out double value)
        {
            if (_values.TryGetValue(key, out var raw) && raw is double d)
            {
                value = d;
                return true;
            }
            value = 0;
            return false;
        }

        public bool TryGetBool(string key, out bool value)
        {
            if (_values.TryGetValue(key, out var raw) && raw is bool b)
            {
                value = b;
                return true;
            }
            value = false;
            return false;
        }
    }
}
