using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zero.Core;

namespace Zero.Services.Attribution
{
    public sealed class MockAttributionService : IAttributionService
    {
        private readonly ILogService _log;

        public string DeviceId => SystemInfo.deviceUniqueIdentifier;

        public MockAttributionService(ILogService log)
        {
            _log = log;
        }

        public UniTask InitializeAsync(CancellationToken ct = default)
        {
            _log.Info($"[ATTRIB:mock] Initialized device={DeviceId}");
            return UniTask.CompletedTask;
        }

        public void TrackEvent(string eventName)
        {
            _log.Info($"[ATTRIB:mock] {eventName}");
        }

        public void TrackEvent(string eventName, IReadOnlyDictionary<string, object> values)
        {
            _log.Info($"[ATTRIB:mock] {eventName} ({values?.Count ?? 0} params)");
        }

        public void TrackPurchase(string productId, decimal amount, string currency)
        {
            _log.Info($"[ATTRIB:mock] purchase {productId} {amount} {currency}");
        }
    }
}
