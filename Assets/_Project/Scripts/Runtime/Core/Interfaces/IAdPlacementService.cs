using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Zero.Core
{
    public interface IAdPlacementService
    {
        bool CanShow(string placementId);
        UniTask<AdShowResult> TryShowAsync(string placementId, CancellationToken ct = default);
        void RegisterPlacement(string placementId, AdType type, TimeSpan cooldown, int sessionCap);
        void NotifyShown(string placementId);
    }
}
