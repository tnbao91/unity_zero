using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

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
