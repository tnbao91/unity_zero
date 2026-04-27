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
