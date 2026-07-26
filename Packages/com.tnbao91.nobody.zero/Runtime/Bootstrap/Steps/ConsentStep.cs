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

        // The legal duty is "do not track without consent", not "do not run without
        // consent". If the consent form cannot load, the correct outcome is a playable
        // game with no personalization — so this degrades rather than blocks.
        //
        // That shifts a real obligation onto whatever you bind for ads / analytics /
        // attribution: they must default to non-personalized when consent is unresolved.
        // Check IBootstrapReport.IsDegraded("Consent") before enabling personalization.
        public override bool IsCritical => false;

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
