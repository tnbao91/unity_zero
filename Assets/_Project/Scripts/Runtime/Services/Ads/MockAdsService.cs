using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using Zero.Core;

namespace Zero.Services.Ads
{
    public sealed class MockAdsService : IAdsService
    {
        private readonly ILogService _log;
        private readonly Subject<AdShowResult> _events = new();
        private readonly Dictionary<AdType, bool> _loaded = new();

        public Observable<AdShowResult> OnAdEvent => _events;

        public MockAdsService(ILogService log)
        {
            _log = log;
            _loaded[AdType.Banner] = true;
            _loaded[AdType.Interstitial] = true;
            _loaded[AdType.Rewarded] = true;
        }

        public UniTask InitializeAsync(CancellationToken ct = default)
        {
            _log.Info("[ADS:mock] Initialized");
            return UniTask.CompletedTask;
        }

        public bool IsReady(AdType type) => _loaded.TryGetValue(type, out var r) && r;

        public async UniTask<AdShowResult> ShowAsync(AdType type, string placementId, CancellationToken ct = default)
        {
            _log.Info($"[ADS:mock] Showing {type} placement={placementId}");
            await UniTask.Delay(500, cancellationToken: ct);
            var result = type == AdType.Rewarded
                ? new AdShowResult(type, AdResult.Rewarded, placementId)
                : new AdShowResult(type, AdResult.Shown, placementId);
            _events.OnNext(result);
            return result;
        }

        public UniTask LoadAsync(AdType type, CancellationToken ct = default)
        {
            _log.Info($"[ADS:mock] Loading {type}");
            _loaded[type] = true;
            return UniTask.CompletedTask;
        }
    }
}
