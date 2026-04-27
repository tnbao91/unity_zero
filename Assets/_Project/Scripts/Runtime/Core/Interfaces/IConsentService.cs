using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace Zero.Core
{
    public enum ConsentStatus
    {
        Unknown,
        NonPersonalized,
        Personalized,
        Denied
    }

    public enum AttStatus
    {
        NotDetermined,
        Restricted,
        Denied,
        Authorized,
        NotApplicable
    }

    public interface IConsentService
    {
        ConsentStatus GdprStatus { get; }
        AttStatus AttStatus { get; }
        UniTask<ConsentStatus> RequestGdprAsync(CancellationToken ct = default);
        UniTask<AttStatus> RequestAttAsync(string trigger, CancellationToken ct = default);
        Observable<ConsentStatus> OnConsentChanged { get; }
    }
}
