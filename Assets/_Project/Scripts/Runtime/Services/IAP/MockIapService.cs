using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using Zero.Core;

namespace Zero.Services.IAP
{
    public sealed class MockIapService : IIAPService
    {
        private readonly ILogService _log;
        private readonly Subject<PurchaseOutcome> _events = new();
        private readonly HashSet<string> _products = new();

        public bool IsInitialized { get; private set; }
        public Observable<PurchaseOutcome> OnPurchase => _events;

        public MockIapService(ILogService log)
        {
            _log = log;
        }

        public UniTask InitializeAsync(IReadOnlyList<string> productIds, CancellationToken ct = default)
        {
            _products.Clear();
            if (productIds != null)
            {
                foreach (var id in productIds) _products.Add(id);
            }
            IsInitialized = true;
            _log.Info($"[IAP:mock] Initialized with {_products.Count} products");
            return UniTask.CompletedTask;
        }

        public async UniTask<PurchaseOutcome> PurchaseAsync(string productId, CancellationToken ct = default)
        {
            _log.Info($"[IAP:mock] Purchasing {productId}");
            await UniTask.Delay(300, cancellationToken: ct);
            var outcome = _products.Contains(productId)
                ? new PurchaseOutcome(PurchaseResult.Purchased, productId, "mock-receipt-" + productId)
                : new PurchaseOutcome(PurchaseResult.Failed, productId, errorMessage: "unknown product");
            _events.OnNext(outcome);
            return outcome;
        }

        public UniTask RestoreAsync(CancellationToken ct = default)
        {
            _log.Info("[IAP:mock] Restore");
            return UniTask.CompletedTask;
        }

        public string GetLocalizedPrice(string productId) => "$0.99";
    }
}
