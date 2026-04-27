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
        public override bool IsCritical => true;

        private readonly ICrashlyticsService _service;

        public CrashlyticsStep(ICrashlyticsService service)
        {
            _service = service;
        }

        protected override UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
            => _service.InitializeAsync(ct);
    }
}
