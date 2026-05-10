using System.Threading;
using Cysharp.Threading.Tasks;
using Zero.Core;

namespace Zero.Services.ReceiptValidator
{
    public sealed class StubReceiptValidator : IReceiptValidator
    {
        // v1 stub — always reports valid. v2 should swap to server-side validation.
        public UniTask<bool> ValidateAsync(string productId, string receipt, CancellationToken ct = default)
        {
            return UniTask.FromResult(true);
        }
    }
}
