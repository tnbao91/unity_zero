using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zero.Core;
using Zero.Infrastructure;

namespace Zero.Bootstrap.Steps
{
    public sealed class RemoteConfigStep : BootstrapStepBase
    {
        public override string Name => "RemoteConfig";

        // Deferred: Defaults are seeded locally, so values are readable before the fetch lands.
        // Subscribe to OnConfigUpdated for the late arrival.
        public override BootstrapPhase Phase => BootstrapPhase.Deferred;
        public override bool IsCritical => false;

        private readonly IRemoteConfigService _service;

        public RemoteConfigStep(IRemoteConfigService service)
        {
            _service = service;
        }

        protected override UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
            => _service.FetchAndActivateAsync(TimeSpan.FromSeconds(3), ct);
    }
}
