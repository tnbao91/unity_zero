using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace Zero.Core
{
    public enum AdType
    {
        Banner,
        Interstitial,
        Rewarded
    }

    public enum AdResult
    {
        Shown,
        Failed,
        Dismissed,
        Rewarded,
        NotReady
    }

    public readonly struct AdShowResult
    {
        public readonly AdType Type;
        public readonly AdResult Result;
        public readonly string PlacementId;
        public readonly string ErrorMessage;

        public AdShowResult(AdType type, AdResult result, string placementId, string errorMessage = null)
        {
            Type = type;
            Result = result;
            PlacementId = placementId;
            ErrorMessage = errorMessage;
        }
    }

    public interface IAdsService
    {
        UniTask InitializeAsync(CancellationToken ct = default);
        bool IsReady(AdType type);
        UniTask<AdShowResult> ShowAsync(AdType type, string placementId, CancellationToken ct = default);
        UniTask LoadAsync(AdType type, CancellationToken ct = default);
        Observable<AdShowResult> OnAdEvent { get; }
    }
}
