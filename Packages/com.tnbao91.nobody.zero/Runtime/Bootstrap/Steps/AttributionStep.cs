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
