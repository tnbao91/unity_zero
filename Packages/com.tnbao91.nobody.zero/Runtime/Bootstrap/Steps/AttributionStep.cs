using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zero.Core;
using Zero.Infrastructure;

namespace Zero.Bootstrap.Steps
{
    public sealed class AttributionStep : BootstrapStepBase
    {
        public override string Name => "Attribution";

        // Deferred: Attribution correctness needs the event within the session, not before frame 1.
        public override BootstrapPhase Phase => BootstrapPhase.Deferred;
        public override bool IsCritical => false;

        private readonly IAttributionService _service;

        public AttributionStep(IAttributionService service)
        {
            _service = service;
        }

        protected override UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
            => _service.InitializeAsync(ct);
    }
}
