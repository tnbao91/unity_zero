# Attribution Service

## Overview

`IAttributionService` records install source (UA campaign, organic vs paid, deep link click) and forwards purchase events for ROAS calculation. The template ships `MockAttributionService` which exposes a stub `DeviceId` and logs all `TrackEvent` / `TrackPurchase` calls. Real impls wrap AppsFlyer, Adjust, Tenjin, Singular, or Branch.

Attribution overlaps significantly with Analytics — they're often the same SDK with different event taxonomies. Most production games hit both simultaneously via a composite (see [analytics.md](analytics.md#multi-sdk-fan-out)).

## Public API

```csharp
namespace Zero.Core
{
    public interface IAttributionService
    {
        UniTask InitializeAsync(CancellationToken ct = default);
        string DeviceId { get; }
        void TrackEvent(string eventName);
        void TrackEvent(string eventName, IReadOnlyDictionary<string, object> values);
        void TrackPurchase(string productId, decimal amount, string currency);
    }
}
```

## Mock behavior

`MockAttributionService` (`Assets/_Project/Scripts/Runtime/Services/Attribution/MockAttributionService.cs`):
- `DeviceId` returns a Guid generated once and persisted via `ISaveService` so it survives restarts.
- `TrackEvent` and `TrackPurchase` write to `Debug.Log`.
- `InitializeAsync` is a no-op.

## Extension Points

### Swap to AppsFlyer

1. Install AppsFlyer Unity SDK ([docs](https://github.com/AppsFlyerSDK/appsflyer-unity-plugin)).
2. Add `AppsFlyerSDK` to `Zero.Services.Attribution.asmdef` references.
3. Implement `AppsFlyerAttributionService : IAttributionService`:

```csharp
using AppsFlyerSDK;

public sealed class AppsFlyerAttributionService : IAttributionService, IAppsFlyerConversionData
{
    private const string DevKey = "YOUR_DEV_KEY";
    private const string AppleAppId = "YOUR_APPLE_APP_ID";

    public string DeviceId => AppsFlyer.getAppsFlyerId();

    public UniTask InitializeAsync(CancellationToken ct = default)
    {
        AppsFlyer.initSDK(DevKey, AppleAppId, this);
        AppsFlyer.startSDK();
        return UniTask.CompletedTask;
    }

    public void TrackEvent(string eventName)
        => AppsFlyer.sendEvent(eventName, new Dictionary<string, string>());

    public void TrackEvent(string eventName, IReadOnlyDictionary<string, object> values)
    {
        var dict = new Dictionary<string, string>(values.Count);
        foreach (var (k, v) in values) dict[k] = v?.ToString() ?? string.Empty;
        AppsFlyer.sendEvent(eventName, dict);
    }

    public void TrackPurchase(string productId, decimal amount, string currency)
    {
        AppsFlyer.sendEvent(AFInAppEvents.PURCHASE, new Dictionary<string, string>
        {
            [AFInAppEvents.CONTENT_ID] = productId,
            [AFInAppEvents.REVENUE] = amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [AFInAppEvents.CURRENCY] = currency,
        });
    }

    // IAppsFlyerConversionData callbacks
    public void onConversionDataSuccess(string s) { /* parse install source */ }
    public void onConversionDataFail(string s) { }
    public void onAppOpenAttribution(string s) { /* parse deep-link */ }
    public void onAppOpenAttributionFailure(string s) { }
}
```

4. Replace the binding in `AttributionServiceInstaller.cs`:
```csharp
builder.RegisterType(typeof(AppsFlyerAttributionService), new[] { typeof(IAttributionService) }, Lifetime.Singleton, Resolution.Lazy);
```

### Swap to Adjust

The Adjust Unity SDK is shape-compatible — replace `AppsFlyer.sendEvent` with `Adjust.trackEvent(new AdjustEvent(token))`. Use Adjust's event tokens (configured in their dashboard) instead of free-form names.

### IDFA / GAID + Consent

`DeviceId` should NOT return raw IDFA / GAID without consent. Tie the value source to `IConsentService.AttStatus`:

```csharp
public string DeviceId => _consent.AttStatus == AttStatus.Authorized
    ? AppsFlyer.getIDFA()
    : _fallbackInstallationId;
```

The fallback installation id is a per-app-install Guid (persisted via `ISaveService`) that lets you stitch sessions for the same install without crossing the privacy line.

### Hook into IAP

`IIAPService.OnPurchase` (R3 Observable) is the natural feed for attribution purchases:

```csharp
[Inject] private IIAPService _iap;
[Inject] private IAttributionService _attribution;

private void Awake()
{
    _iap.OnPurchase
        .Where(o => o.Result == PurchaseResult.Purchased)
        .Subscribe(o =>
        {
            // Look up amount + currency from your local product catalog.
            var (amount, currency) = LookupPrice(o.ProductId);
            _attribution.TrackPurchase(o.ProductId, amount, currency);
        });
}
```

## Examples

```csharp
[Inject] private IAttributionService _attribution;

private void OnTutorialComplete()
{
    _attribution.TrackEvent("tutorial_complete");
}

private void OnRevenueEvent(string sku, decimal usd)
{
    _attribution.TrackPurchase(sku, usd, "USD");
}
```

## Known Limitations

- **Mock `DeviceId` is a generated Guid**, not a real IDFA / GAID — use the mock for local dev only.
- **No conversion data callback in `IAttributionService`.** Install-source data (campaign, ad set) lands via SDK-specific callbacks (`IAppsFlyerConversionData.onConversionDataSuccess`). Surface it via the bus or a dedicated `Observable<ConversionData>` in your impl.
- **`TrackPurchase` takes `decimal`** but SDK APIs typically want `double` — your wrapper converts. Decimal avoids floating-point rounding for tax / cents math.

## Design Rationale

- **Separate from `IAnalyticsService`** because attribution events are revenue-bearing and need exactness (no sampling, no batching that could drop). Composite wrapping is supported but the interfaces stay distinct.
- **`DeviceId` as a property, not a method** — it's lookup-cheap on every SDK and consumers read it constantly (e.g. for support-ticket correlation).
- **No `OnAttributionLoaded` event in the interface** because every SDK names this differently and the typed payload is SDK-specific. Surface via `IEventBus` in your impl with a typed event POCO if multiple subscribers care.
