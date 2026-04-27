using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zero.Core;

namespace Zero.Bootstrap
{
    public sealed class BootstrapPipeline
    {
        private readonly IReadOnlyList<IBootstrapStep> _steps;
        private readonly ILogService _log;

        public BootstrapPipeline(IReadOnlyList<IBootstrapStep> steps, ILogService log)
        {
            _steps = steps;
            _log = log;
        }

        public async UniTask RunAsync(IProgress<float> overallProgress, CancellationToken ct)
        {
            for (int i = 0; i < _steps.Count; i++)
            {
                var step = _steps[i];
                ct.ThrowIfCancellationRequested();

                _log.Info($"[Bootstrap] Step {i + 1}/{_steps.Count}: {step.Name}");

                try
                {
                    await step.ExecuteAsync(null, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _log.Error(ex, $"[Bootstrap] Step '{step.Name}' failed");
                    if (step.IsCritical)
                    {
                        throw;
                    }
                }

                overallProgress?.Report((i + 1f) / _steps.Count);
            }
        }
    }
}
