using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zero.Core;
using Zero.Infrastructure;

namespace Zero.Bootstrap.Steps
{
    public sealed class ConsentStep : BootstrapStepBase
    {
        public override string Name => "Consent";
        public override bool IsCritical => true;

        private readonly IConsentService _service;

        public ConsentStep(IConsentService service)
        {
            _service = service;
        }

        protected override async UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
        {
            await _service.RequestGdprAsync(ct);
            await _service.RequestAttAsync("boot", ct);
        }
    }
}
