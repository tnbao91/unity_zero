# Consent Service

## Overview

`IConsentService` manages the GDPR consent prompt (EU) and the iOS App Tracking Transparency prompt. The template ships `MockConsentService` — `RequestGdprAsync` returns `Personalized` and `RequestAttAsync` returns `Authorized` immediately. **No copy is provided** — privacy text is jurisdiction-dependent and consumer-supplied.

Real impls wrap Google's User Messaging Platform (UMP, IAB TCF v2 compliant) on Android, and `ATTrackingManager` on iOS.

## Public API

```csharp
namespace Zero.Core
{
    public enum ConsentStatus { Unknown, NonPersonalized, Personalized, Denied }
    public enum AttStatus    { NotDetermined, Restricted, Denied, Authorized, NotApplicable }

    public interface IConsentService
    {
        ConsentStatus GdprStatus { get; }
        AttStatus AttStatus { get; }
        UniTask<ConsentStatus> RequestGdprAsync(CancellationToken ct = default);
        UniTask<AttStatus> RequestAttAsync(string trigger, CancellationToken ct = default);
        Observable<ConsentStatus> OnConsentChanged { get; }
    }
}
```

## Mock behavior

`MockConsentService` (`Assets/_Project/Scripts/Runtime/Services/Consent/MockConsentService.cs`):
- `GdprStatus` returns `Personalized` after `RequestGdprAsync` is called once.
- `AttStatus` returns `Authorized` after `RequestAttAsync`.
- `OnConsentChanged` emits when status changes.

## Extension Points

### Swap to Google UMP (Android + iOS)

1. Install [Google UMP](https://developers.google.com/admob/unity/privacy) via the Unity Mobile Ads SDK.
2. Add the SDK asmdef to `Zero.Services.Consent.asmdef` references.
3. Implement `UmpConsentService : IConsentService`:

```csharp
using GoogleMobileAds.Ump.Api;

public sealed class UmpConsentService : IConsentService
{
    private readonly Subject<ConsentStatus> _changed = new();
    public ConsentStatus GdprStatus { get; private set; }
    public AttStatus AttStatus { get; private set; }
    public Observable<ConsentStatus> OnConsentChanged => _changed;

    public UniTask<ConsentStatus> RequestGdprAsync(CancellationToken ct = default)
    {
        var tcs = new UniTaskCompletionSource<ConsentStatus>();
        var req = new ConsentRequestParameters();
        ConsentInformation.Update(req, error =>
        {
            if (error != null)
            {
                tcs.TrySetResult(ConsentStatus.Unknown);
                return;
            }
            ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
            {
                GdprStatus = MapStatus(ConsentInformation.ConsentStatus);
                _changed.OnNext(GdprStatus);
                tcs.TrySetResult(GdprStatus);
            });
        });
        return tcs.Task;
    }

    public UniTask<AttStatus> RequestAttAsync(string trigger, CancellationToken ct = default)
    {
        // Delegated to iOS-only path — see iOS ATT impl below.
        return UniTask.FromResult(AttStatus.NotApplicable);
    }

    private static ConsentStatus MapStatus(GoogleMobileAds.Ump.Api.ConsentStatus s) => s switch
    {
        GoogleMobileAds.Ump.Api.ConsentStatus.Obtained => ConsentStatus.Personalized,
        GoogleMobileAds.Ump.Api.ConsentStatus.NotRequired => ConsentStatus.NonPersonalized,
        _ => ConsentStatus.Unknown,
    };
}
```

4. Replace the binding in `ConsentServiceInstaller.cs`:
```csharp
builder.RegisterType(typeof(UmpConsentService), new[] { typeof(IConsentService) }, Lifetime.Singleton, Resolution.Lazy);
```

### iOS ATT (App Tracking Transparency)

`Unity.Advertisement.IosSupport` ships `ATTrackingStatusBinding`:
```csharp
ATTrackingStatusBinding.RequestAuthorizationTracking();
var status = ATTrackingStatusBinding.GetAuthorizationTrackingStatus();
```
Wrap behind `#if UNITY_IOS` and call from `RequestAttAsync` with the user-shown rationale `trigger` string passed for analytics.

### Trigger ATT at the right moment

ATT is **once-per-install**. Show after the user has experienced enough value to make an informed choice — not on first launch. Common triggers: first level complete, first popup dismiss, first reward-claim. Pass the trigger name into `RequestAttAsync` so analytics can correlate.

## Examples

Bootstrap step (`ConsentStep` in the template) calls `RequestGdprAsync` for EU users (detect via `IDeviceProfileService.RegionCode` or the SDK's own check). ATT is consumer-triggered.

```csharp
[Inject] private IConsentService _consent;
[Inject] private IAdsService _ads;

private async UniTask AfterFirstLevel()
{
    var att = await _consent.RequestAttAsync("level_1_complete");
    if (att == AttStatus.Authorized)
        _ads.LoadAsync(AdType.Rewarded).Forget();
}
```

Subscribing to changes:
```csharp
_consent.OnConsentChanged.Subscribe(status =>
{
    _analytics.SetUserProperty("consent_status", status.ToString());
});
```

## Known Limitations

- **No privacy copy ships.** Consumer drafts the text per jurisdiction.
- **No partial consent.** This interface is binary-per-purpose. Detailed IAB TCF v2 string handling lives inside the impl — exposing it through this interface would force consumers to think in TCF, which is ad-network-specific.
- **Mock returns Personalized / Authorized** to simplify dev iteration. Flip to `Denied` paths in your impl tests if you need consent-rejected coverage.

## Design Rationale

- **Two enums, one interface.** GDPR and ATT are different regulatory regimes with overlapping concerns. Splitting into `IGdprService` + `IAttService` would force every consumer to inject both; consolidating keeps the API surface small.
- **`RequestAttAsync` takes a `trigger` string** — not enforced by Apple, but useful for analytics to understand which "value moment" yielded which consent rate.
- **`OnConsentChanged` is `Observable<ConsentStatus>`** — emits GDPR changes only. ATT is once-per-install, so a stream isn't useful.
