using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using Zero.Core;

namespace Zero.Services.Consent
{
    public sealed class MockConsentService : IConsentService
    {
        private readonly ILogService _log;
        private readonly Subject<ConsentStatus> _consentChanged = new();

        public ConsentStatus GdprStatus { get; private set; } = ConsentStatus.Unknown;
        public AttStatus AttStatus { get; private set; } = AttStatus.NotDetermined;
        public Observable<ConsentStatus> OnConsentChanged => _consentChanged;

        public MockConsentService(ILogService log)
        {
            _log = log;
        }

        public async UniTask<ConsentStatus> RequestGdprAsync(CancellationToken ct = default)
        {
            await UniTask.Delay(100, cancellationToken: ct);
            GdprStatus = ConsentStatus.Personalized;
            _log.Info($"[CONSENT:mock] GDPR -> {GdprStatus}");
            _consentChanged.OnNext(GdprStatus);
            return GdprStatus;
        }

        public async UniTask<AttStatus> RequestAttAsync(string trigger, CancellationToken ct = default)
        {
            await UniTask.Delay(100, cancellationToken: ct);
            AttStatus = AttStatus.Authorized;
            _log.Info($"[CONSENT:mock] ATT trigger='{trigger}' -> {AttStatus}");
            return AttStatus;
        }
    }
}
