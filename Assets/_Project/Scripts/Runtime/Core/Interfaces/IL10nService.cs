using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace Zero.Core
{
    // Locale identifier kept as string to avoid coupling Zero.Core to Unity.Localization.
    // Real impl converts to LocaleIdentifier internally; Mock uses the string directly.
    public interface IL10nService
    {
        string Get(string key, params object[] args);
        Observable<string> OnLocaleChanged { get; }
        UniTask SetLocaleAsync(string locale, CancellationToken ct = default);
        string CurrentLocale { get; }
    }
}
