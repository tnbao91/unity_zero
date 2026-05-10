using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace Zero.Core
{
    public interface IRemoteConfigService
    {
        UniTask<bool> FetchAndActivateAsync(TimeSpan timeout, CancellationToken ct = default);
        T GetVariant<T>(string key, T defaultValue);
        bool TryGetString(string key, out string value);
        bool TryGetLong(string key, out long value);
        bool TryGetDouble(string key, out double value);
        bool TryGetBool(string key, out bool value);
        Observable<Unit> OnConfigUpdated { get; }
    }
}
