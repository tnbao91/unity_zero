# Ads Service

## Overview

`IAdsService` is the SDK adapter for ad networks (AppLovin MAX, Unity Ads, IronSource, Google AdMob). The template ships `MockAdsService` which auto-completes every show with `AdResult.Rewarded` after a short delay — useful for prototyping reward flows without a network. Real impls wrap a single mediation SDK or a custom waterfall.

`IAdsService` is the network adapter. **`IAdPlacementService` is a separate, already-real abstraction** that maps gameplay placements (`"after_level_complete"`, `"continue_offer"`) to ad types and frequency caps. The placement layer is template-stable; only the SDK adapter is mock.

## Public API

```csharp
namespace Zero.Core
{
    public enum AdType { Banner, Interstitial, Rewarded }
    public enum AdResult { Shown, Failed, Dismissed, Rewarded, NotReady }

    public readonly struct AdShowResult
    {
        public readonly AdType Type;
        public readonly AdResult Result;
        public readonly string PlacementId;
        public readonly string ErrorMessage;
        public AdShowResult(AdType type, AdResult result, string placementId, string errorMessage = null);
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
```

## Mock behavior

`MockAdsService` (`Assets/_Project/Scripts/Runtime/Services/Ads/MockAdsService.cs`):
- `IsReady(_)` returns `true` after a brief simulated load.
- `ShowAsync` returns `Shown` for Banner/Interstitial and `Rewarded` for Rewarded after ~500ms simulated playback.
- `OnAdEvent` emits the same result.
- Useful for prototyping reward flows: a `wallet add coins 100` happens regardless of whether a real ad would fill.

## Extension Points

### Swap to AppLovin MAX

1. Install [AppLovin MAX Unity SDK](https://developers.applovin.com/en/unity/overview/integration/) via UPM or the AppLovin downloader.
2. Add `MaxSdk` (or whatever asmdef the package exposes) to `Zero.Services.Ads.asmdef` references.
3. Implement `MaxAdsService : IAdsService`:

```csharp
public sealed class MaxAdsService : IAdsService
{
    private const string SdkKey = "YOUR_SDK_KEY";
    private const string RewardedAdUnitId = "YOUR_REWARDED_UNIT_ID";
    private readonly Subject<AdShowResult> _events = new();
    public Observable<AdShowResult> OnAdEvent => _events;

    public UniTask InitializeAsync(CancellationToken ct = default)
    {
        var tcs = new UniTaskCompletionSource();
        MaxSdkCallbacks.OnSdkInitializedEvent += _ => tcs.TrySetResult();
        MaxSdk.SetSdkKey(SdkKey);
        MaxSdk.InitializeSdk();
        return tcs.Task;
    }

    public bool IsReady(AdType type) => type switch
    {
        AdType.Rewarded => MaxSdk.IsRewardedAdReady(RewardedAdUnitId),
        // ... interstitial / banner.
        _ => false,
    };

    public async UniTask<AdShowResult> ShowAsync(AdType type, string placementId, CancellationToken ct = default)
    {
        var tcs = new UniTaskCompletionSource<AdShowResult>();
        // Wire MaxSdkCallbacks (OnRewardedAdReceivedRewardEvent / OnRewardedAdFailedToDisplayEvent / OnRewardedAdHiddenEvent)
        // to TrySetResult; show the ad; await tcs.Task.
        // ...
        return await tcs.Task;
    }

    public UniTask LoadAsync(AdType type, CancellationToken ct = default)
    {
        if (type == AdType.Rewarded) MaxSdk.LoadRewardedAd(RewardedAdUnitId);
        return UniTask.CompletedTask;
    }
}
```

4. Replace the binding in `AdsServiceInstaller.cs`:
```csharp
builder.RegisterType(typeof(MaxAdsService), new[] { typeof(IAdsService) }, Lifetime.Singleton, Resolution.Lazy);
```

### Swap to Unity LevelPlay (formerly IronSource)

Similar shape; LevelPlay's namespace is `Unity.Services.LevelPlay`. Different callback API but same ctor injection pattern.

### Use `IAdPlacementService` to gate frequency

The placement service (already real) handles "show interstitial at most every 90 seconds" and "block ads if user has no-ads IAP". Gameplay code calls the placement service, which calls `IAdsService` internally:

```csharp
[Inject] private IAdPlacementService _placement;

private async UniTask OnLevelComplete()
{
    await _placement.MaybeShowInterstitialAsync("after_level_complete");
}
```

Don't call `IAdsService.ShowAsync` directly from gameplay — placement is where frequency / no-ads rules live.

### Pre-load before placements

```csharp
[Inject] private IAdsService _ads;

private async UniTask Start()
{
    await _ads.InitializeAsync();
    _ads.LoadAsync(AdType.Interstitial).Forget();
    _ads.LoadAsync(AdType.Rewarded).Forget();
}
```

Real SDKs auto-reload after a show; the explicit initial load reduces first-impression latency.

## Examples

Reward flow:
```csharp
public async UniTask<bool> WatchAdForReward()
{
    var result = await _ads.ShowAsync(AdType.Rewarded, "double_coins");
    return result.Result == AdResult.Rewarded;
}
```

## Known Limitations

- **Mock auto-rewards every show** — testing the "ad failed, refuse reward" path needs a real impl or a wrapper that randomizes failures.
- **No banner positioning** in the interface — `ShowAsync(AdType.Banner, ...)` shows at the SDK's default position. Add a `BannerPosition` enum to `IAdsService` if your game needs more control.
- **Single ad unit per type** in most real impls; the interface doesn't surface `placementId → ad unit` mapping, that's hidden inside the wrapper. Most mediation SDKs handle waterfall internally.

## Design Rationale

- **`AdShowResult` as `readonly struct`** — fired through `Observable<T>` and returned from `ShowAsync`; keeps allocations zero in the hot path.
- **Separate `IAdsService` (network adapter) from `IAdPlacementService` (gameplay rules)** — gameplay never reasons about ad networks; networks never reason about no-ads IAP. Two responsibilities, two interfaces.
- **Mock auto-rewards** rather than randomizing — predictable for dev iteration. Failure-path testing is the wrapper's job.
- **`OnAdEvent` Observable** rather than callbacks — composable (filter to `AdType.Rewarded`, throttle, count for cap analytics) and consistent with R3 idioms elsewhere in the template.
