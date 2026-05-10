using System.Threading;
using Cysharp.Threading.Tasks;

namespace Zero.Core
{
    public interface IReceiptValidator
    {
        UniTask<bool> ValidateAsync(string productId, string receipt, CancellationToken ct = default);
    }
}
