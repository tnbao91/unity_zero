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

        // The most arguable non-critical step in the pipeline. If Addressables fails to
        // initialize, nothing content-driven will load — so blocking here is defensible.
        // It is still wrong for this template: aborting leaves the player on a splash
        // screen (no retry UI ships), whereas continuing lets the game show its own
        // error where it has UI to do it, and lets IAssetService.LoadAsync retry at the
        // call site where a transient catalog fetch can actually recover.
        //
        // Gate content on IBootstrapReport.IsDegraded("Asset") if you need to branch.
        // Retried more than the default, because this is the one worth retrying.
        public override bool IsCritical => false;
        public override int MaxRetries => 2;

        private readonly IAssetService _service;

        public AssetStep(IAssetService service)
        {
            _service = service;
        }

        protected override UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
            => _service.InitializeAsync(ct);
    }
}
