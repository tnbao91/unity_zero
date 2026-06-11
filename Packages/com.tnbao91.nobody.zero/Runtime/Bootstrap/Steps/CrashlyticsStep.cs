using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zero.Core;
using Zero.Infrastructure;

namespace Zero.Bootstrap.Steps
{
    public sealed class CrashlyticsStep : BootstrapStepBase
    {
        public override string Name => "Crashlytics";

        // First in pipeline order so later failures get reported (ordering), but a
        // crash-reporter outage must never block app launch (criticality) — aborting
        // launch produces zero reports either way. 5s caps the splash-screen cost
        // when a real SDK hangs; see PITFALLS "swap a real SDK into a bootstrap step".
        public override bool IsCritical => false;
        public override TimeSpan Timeout => TimeSpan.FromSeconds(5);

        private readonly ICrashlyticsService _service;

        public CrashlyticsStep(ICrashlyticsService service)
        {
            _service = service;
        }

        protected override UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
            => _service.InitializeAsync(ct);
    }
}
