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

        // Blocking by default so an existing consumer step keeps the semantics it had before
        // phases existed. Almost every step should override this to Deferred — see
        // docs/architecture/bootstrap-pipeline.md §"Phases".
        public virtual BootstrapPhase Phase => BootstrapPhase.Blocking;

        // 10s, not 30s: this is a mobile boot path. A step that needs longer than this is
        // telling you it belongs in the deferred phase. Zero disables the deadline entirely
        // (ConsentStep uses that — a real consent dialog waits on a human).
        public virtual TimeSpan Timeout => TimeSpan.FromSeconds(10);
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
