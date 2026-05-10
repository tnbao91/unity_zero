using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zero.Core;
using Zero.Infrastructure;

namespace Zero.Bootstrap.Steps
{
    public sealed class AdsStep : BootstrapStepBase
    {
        public override string Name => "Ads";
        public override bool IsCritical => false;

        private readonly IAdsService _ads;
        private readonly IAdPlacementService _placement;

        public AdsStep(IAdsService ads, IAdPlacementService placement)
        {
            _ads = ads;
            _placement = placement;
        }

        protected override async UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
        {
            await _ads.InitializeAsync(ct);

            // Default placements — games can re-register or add more from Meta layer.
            _placement.RegisterPlacement("level_complete", AdType.Interstitial, TimeSpan.FromSeconds(60), sessionCap: 10);
            _placement.RegisterPlacement("revive", AdType.Rewarded, TimeSpan.FromSeconds(0), sessionCap: 99);
            _placement.RegisterPlacement("double_reward", AdType.Rewarded, TimeSpan.FromSeconds(0), sessionCap: 99);
        }
    }
}
