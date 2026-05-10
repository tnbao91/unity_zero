using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zero.Core;
using Zero.Infrastructure;

namespace Zero.Bootstrap.Steps
{
    public sealed class VersionCheckStep : BootstrapStepBase
    {
        public override string Name => "VersionCheck";
        public override bool IsCritical => false;

        private readonly IVersionCheckService _service;

        public VersionCheckStep(IVersionCheckService service)
        {
            _service = service;
        }

        protected override async UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
        {
            await _service.CheckAsync(ct);
        }
    }
}
