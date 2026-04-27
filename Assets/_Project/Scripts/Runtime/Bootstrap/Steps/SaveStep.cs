using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zero.Core;
using Zero.Infrastructure;

namespace Zero.Bootstrap.Steps
{
    public sealed class SaveStep : BootstrapStepBase
    {
        public override string Name => "Save";
        public override bool IsCritical => false;

        private readonly ISaveService _service;

        public SaveStep(ISaveService service)
        {
            _service = service;
        }

        protected override UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
            => _service.LoadAsync(ct);
    }
}
