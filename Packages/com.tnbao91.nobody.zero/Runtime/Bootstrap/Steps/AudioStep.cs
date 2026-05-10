using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zero.Core;
using Zero.Infrastructure;

namespace Zero.Bootstrap.Steps
{
    public sealed class AudioStep : BootstrapStepBase
    {
        public override string Name => "Audio";
        public override bool IsCritical => false;

        private readonly IAudioService _service;

        public AudioStep(IAudioService service)
        {
            _service = service;
        }

        protected override UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
            => _service.InitializeAsync(ct);
    }
}
