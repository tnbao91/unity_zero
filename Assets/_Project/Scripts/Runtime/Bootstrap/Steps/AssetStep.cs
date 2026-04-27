using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zero.Core;
using Zero.Infrastructure;

namespace Zero.Bootstrap.Steps
{
    public sealed class AssetStep : BootstrapStepBase
    {
        public override string Name => "Asset";
        public override bool IsCritical => true;

        private readonly IAssetService _service;

        public AssetStep(IAssetService service)
        {
            _service = service;
        }

        protected override UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
            => _service.InitializeAsync(ct);
    }
}
