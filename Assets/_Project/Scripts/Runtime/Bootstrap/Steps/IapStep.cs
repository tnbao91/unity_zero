using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zero.Core;
using Zero.Infrastructure;

namespace Zero.Bootstrap.Steps
{
    public sealed class IapStep : BootstrapStepBase
    {
        public override string Name => "IAP";
        public override bool IsCritical => false;

        private static readonly string[] DefaultProductIds =
        {
            "coins_100",
            "coins_500",
            "remove_ads",
        };

        private readonly IIAPService _service;

        public IapStep(IIAPService service)
        {
            _service = service;
        }

        protected override UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
            => _service.InitializeAsync(DefaultProductIds, ct);
    }
}
