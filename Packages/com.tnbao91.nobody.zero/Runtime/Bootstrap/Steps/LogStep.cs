using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zero.Core;
using Zero.Infrastructure;

namespace Zero.Bootstrap.Steps
{
    public sealed class LogStep : BootstrapStepBase
    {
        public override string Name => "Log";

        // Blocking — a no-op that writes one line. Kept as a step only because step names are
        // public API via BootstrapStepComposer anchors — deleting it would break consumers.
        public override BootstrapPhase Phase => BootstrapPhase.Blocking;

        private readonly ILogService _log;

        public LogStep(ILogService log)
        {
            _log = log;
        }

        protected override UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
        {
            _log.Info("[Bootstrap] Log service online.");
            return UniTask.CompletedTask;
        }
    }
}
