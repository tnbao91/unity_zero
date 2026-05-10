using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zero.Core;
using Zero.Infrastructure;

namespace Zero.Bootstrap.Steps
{
    public sealed class TimeStep : BootstrapStepBase
    {
        public override string Name => "Time";
        public override bool IsCritical => false;

        private readonly ITimeService _service;

        public TimeStep(ITimeService service)
        {
            _service = service;
        }

        protected override UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
            => _service.SyncAsync(ct);
    }
}
