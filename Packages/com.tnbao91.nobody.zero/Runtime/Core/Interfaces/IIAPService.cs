using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace Zero.Core
{
    public enum PurchaseResult
    {
        Pending,
        Purchased,
        Failed,
        Restored,
        Cancelled
    }

    public readonly struct PurchaseOutcome
    {
        public readonly PurchaseResult Result;
        public readonly string ProductId;
        public readonly string Receipt;
        public readonly string ErrorMessage;

        public PurchaseOutcome(PurchaseResult result, string productId, string receipt = null, string errorMessage = null)
        {
            Result = result;
            ProductId = productId;
            Receipt = receipt;
            ErrorMessage = errorMessage;
        }
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
