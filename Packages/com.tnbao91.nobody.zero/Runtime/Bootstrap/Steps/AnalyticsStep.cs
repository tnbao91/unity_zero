using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zero.Core;
using Zero.Infrastructure;

namespace Zero.Bootstrap.Steps
{
    public sealed class AnalyticsStep : BootstrapStepBase
    {
        public override string Name => "Analytics";

        // Deferred: Real SDKs buffer events before init. Start it early, never await it.
        public override BootstrapPhase Phase => BootstrapPhase.Deferred;
        public override bool IsCritical => false;

        private readonly IAnalyticsService _service;

        public AnalyticsStep(IAnalyticsService service)
        {
            _service = service;
        }

        protected override UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
            => _service.InitializeAsync(ct);
    }
}
