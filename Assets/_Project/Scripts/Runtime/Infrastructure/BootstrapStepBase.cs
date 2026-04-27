using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zero.Core;

namespace Zero.Infrastructure
{
    public abstract class BootstrapStepBase : IBootstrapStep
    {
        public abstract string Name { get; }
        public virtual bool IsCritical => false;

        public async UniTask ExecuteAsync(IProgress<float> progress, CancellationToken ct)
        {
            progress?.Report(0f);
            try
            {
                await OnExecuteAsync(progress, ct);
            }
            finally
            {
                progress?.Report(1f);
            }
        }

        protected abstract UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct);
    }
}
