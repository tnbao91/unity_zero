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

        // Deferred: A real UMP/ATT flow shows modal system UI. It must never sit between the
        // player and the game — and Apple's own guidance is to ask at a value moment.
        public override BootstrapPhase Phase => BootstrapPhase.Deferred;

        // No deadline. A real consent dialog is waiting on a human being; a step timeout
        // would cancel it while the player is mid-read. Zero means "no timeout" to the
        // pipeline. Safe only because this step is deferred — it blocks nobody.
        public override TimeSpan Timeout => TimeSpan.Zero;

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
