using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zero.Core;
using Zero.Infrastructure;
using Zero.UI;

namespace Zero.Bootstrap.Steps
{
    public sealed class UIStep : BootstrapStepBase
    {
        public override string Name => "UI";
        public override bool IsCritical => false;

        private readonly IUIService _service;

        public UIStep(IUIService service)
        {
            _service = service;
        }

        protected override UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
            => _service.InitializeAsync(ct);
    }
}
