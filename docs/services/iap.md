# IAP Service

## Overview

`IIAPService` exposes in-app purchase flows: initialize with a product id list, kick off `PurchaseAsync`, restore prior purchases, read localized prices for storefront display. The template ships `MockIapService` which auto-completes every purchase with `PurchaseResult.Purchased` after a short delay — useful for prototyping reward grants without storefront wiring.

Real impls wrap `com.unity.purchasing` (Unity IAP, version 5.2.1 already in `Packages/manifest.json`).

## Public API

```csharp
namespace Zero.Core
{
    public enum PurchaseResult { Pending, Purchased, Failed, Restored, Cancelled }

    public readonly struct PurchaseOutcome
    {
        public readonly PurchaseResult Result;
        public readonly string ProductId;
        public readonly string Receipt;
        public readonly string ErrorMessage;
        public PurchaseOutcome(PurchaseResult result, string productId, string receipt = null, string errorMessage = null);
    }

    public interface IIAPService
    {
        bool IsInitialized { get; }
        UniTask InitializeAsync(IReadOnlyList<string> productIds, CancellationToken ct = default);
        UniTask<PurchaseOutcome> PurchaseAsync(string productId, CancellationToken ct = default);
        UniTask RestoreAsync(CancellationToken ct = default);
        string GetLocalizedPrice(string productId);
        Observable<PurchaseOutcome> OnPurchase { get; }
    }
}
```

## Mock behavior

`MockIapService` (`Assets/_Project/Scripts/Runtime/Services/IAP/MockIapService.cs`):
- `InitializeAsync` returns `UniTask.CompletedTask` and flips `IsInitialized = true`.
- `PurchaseAsync` waits ~500ms then returns `PurchaseOutcome(Purchased, productId, receipt: "MOCK_RECEIPT")` and emits the same on `OnPurchase`.
- `GetLocalizedPrice` returns a stub like `"$0.99"`.
- `RestoreAsync` is a no-op.

## Extension Points

### Swap to Unity IAP (`com.unity.purchasing`)

1. Confirm `com.unity.purchasing` (5.2.1+) is in `Packages/manifest.json` (already in this template).
2. Add `Unity.Services.Core`, `Unity.Purchasing` to `Zero.Services.IAP.asmdef` references.
3. Implement `UnityIapService : IIAPService, IDetailedStoreListener`:

```csharp
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

public sealed class UnityIapService : IIAPService, IDetailedStoreListener
{
    private IStoreController _store;
    private readonly Subject<PurchaseOutcome> _purchases = new();
    private UniTaskCompletionSource _initTcs;
    private UniTaskCompletionSource<PurchaseOutcome> _pendingPurchase;

    public bool IsInitialized => _store != null;
    public Observable<PurchaseOutcome> OnPurchase => _purchases;

    public UniTask InitializeAsync(IReadOnlyList<string> productIds, CancellationToken ct = default)
    {
        _initTcs = new UniTaskCompletionSource();
        var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
        foreach (var id in productIds)
            builder.AddProduct(id, ProductType.Consumable);
        UnityPurchasing.Initialize(this, builder);
        return _initTcs.Task;
    }

    public UniTask<PurchaseOutcome> PurchaseAsync(string productId, CancellationToken ct = default)
    {
        _pendingPurchase = new UniTaskCompletionSource<PurchaseOutcome>();
        _store.InitiatePurchase(productId);
        return _pendingPurchase.Task;
    }

    public UniTask RestoreAsync(CancellationToken ct = default)
    {
        // iOS only — Android is automatic on init.
#if UNITY_IOS
        var apple = _store.extensions.GetExtension<IAppleExtensions>();
        apple.RestoreTransactions((ok, _) => { /* emit Restored outcomes via OnProcessPurchase */ });
#endif
        return UniTask.CompletedTask;
    }

    public string GetLocalizedPrice(string productId)
        => _store?.products.WithID(productId)?.metadata?.localizedPriceString ?? "—";

    // IDetailedStoreListener callbacks.
    public void OnInitialized(IStoreController controller, IExtensionProvider _)
    {
        _store = controller;
        _initTcs?.TrySetResult();
    }
    public void OnInitializeFailed(InitializationFailureReason _, string msg)
        => _initTcs?.TrySetException(new Exception(msg));
    public void OnInitializeFailed(InitializationFailureReason r) => OnInitializeFailed(r, r.ToString());

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        var outcome = new PurchaseOutcome(
            PurchaseResult.Purchased, args.purchasedProduct.definition.id,
            args.purchasedProduct.receipt);
        _pendingPurchase?.TrySetResult(outcome);
        _purchases.OnNext(outcome);
        return PurchaseProcessingResult.Complete;
    }

    public void OnPurchaseFailed(Product p, PurchaseFailureDescription failure)
    {
        var outcome = new PurchaseOutcome(
            failure.reason == PurchaseFailureReason.UserCancelled
                ? PurchaseResult.Cancelled : PurchaseResult.Failed,
            p.definition.id, errorMessage: failure.message);
        _pendingPurchase?.TrySetResult(outcome);
        _purchases.OnNext(outcome);
    }
    public void OnPurchaseFailed(Product p, PurchaseFailureReason r)
        => OnPurchaseFailed(p, new PurchaseFailureDescription(p.definition.id, r, r.ToString()));
}
```

4. Replace the binding in `IapServiceInstaller.cs`:
```csharp
builder.RegisterType(typeof(UnityIapService), new[] { typeof(IIAPService) }, Lifetime.Singleton, Resolution.Lazy);
```

### Validate receipts

Pair `IIAPService` with `IReceiptValidator` ([receipt-validator.md](receipt-validator.md)). The flow:

```csharp
[Inject] private IIAPService _iap;
[Inject] private IReceiptValidator _validator;

public async UniTask<bool> Buy(string productId)
{
    var outcome = await _iap.PurchaseAsync(productId);
    if (outcome.Result != PurchaseResult.Purchased) return false;

    var valid = await _validator.ValidateAsync(productId, outcome.Receipt);
    if (!valid)
    {
        // Treat as fraud — don't grant. Optionally crash-report.
        return false;
    }

    GrantReward(productId);
    return true;
}
```

### Track purchases for attribution + analytics

`OnPurchase` is the single source of truth — fan out:

```csharp
_iap.OnPurchase
    .Where(o => o.Result == PurchaseResult.Purchased)
    .Subscribe(o =>
    {
        _attribution.TrackPurchase(o.ProductId, GetPriceFromCatalog(o.ProductId), "USD");
        _analytics.LogEvent("iap_purchased", new Dictionary<string, object> { ["product_id"] = o.ProductId });
    });
```

## Examples

```csharp
[Inject] private IIAPService _iap;

private async UniTask Start()
{
    await _iap.InitializeAsync(new[] { "coins_100", "coins_500", "remove_ads" });
    _priceLabel.text = _iap.GetLocalizedPrice("coins_100");
}

public async UniTask OnBuyCoins100()
{
    var outcome = await _iap.PurchaseAsync("coins_100");
    if (outcome.Result == PurchaseResult.Purchased)
        _wallet.Add("coins", 100);
}
```

## Known Limitations

- **Mock returns "MOCK_RECEIPT"** — `IReceiptValidator` must accept the mock receipt or fail open during dev. The shipped `StubReceiptValidator` accepts everything.
- **No subscription lifecycle in the interface.** Recurring subscriptions (renewal, cancellation, upgrade) need richer types — extend `PurchaseOutcome` or add a separate `ISubscriptionService` if your game uses them.
- **No "promo code" path.** Apple promo codes / Google promotional offers fire through `OnProcessPurchase` like a regular purchase; downstream code should treat them identically.
- **`GetLocalizedPrice` returns string only** — for sorting / total calculations, parse with `decimal.Parse` and the user's culture, or augment your impl to expose `decimal LocalizedPriceAsDecimal(string id)`.

## Design Rationale

- **`PurchaseOutcome` over throw** — Cancelled is normal user behavior, not exceptional. Pattern-match on `.Result` instead of try/catch.
- **`OnPurchase` Observable + `PurchaseAsync` return value** — both surfaces because some flows want fire-and-await (button → reward) and some want global subscription (analytics). Bus-event-only would force button code to subscribe, race the result, and dispose. Both = ergonomic.
- **`InitializeAsync` takes the product list** rather than reading from a config — avoids a global "where is the product catalog" lookup; consumer assembles the list at the call site.
- **`Restore` is async + no-op-ish** because Android auto-restores on init; iOS needs an explicit call wired to a "Restore Purchases" button. The interface handles both.
