using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zero.Core;

namespace Zero.Infrastructure
{
    // Base for all IBootstrapStep implementations. Defaults applied here (rather than as
    // C# 8 default interface members) to keep IL2CPP happy and to let concrete steps
    // override Timeout / MaxRetries with `public override TimeSpan Timeout => ...;`.
    public abstract class BootstrapStepBase : IBootstrapStep
    {
        public abstract string Name { get; }
        public virtual bool IsCritical => false;
        public virtual TimeSpan Timeout => TimeSpan.FromSeconds(30);
        public virtual int MaxRetries => 1;

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
