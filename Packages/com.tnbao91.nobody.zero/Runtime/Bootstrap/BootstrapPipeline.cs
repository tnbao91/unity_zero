using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zero.Core;
using Zero.Core.Events;

namespace Zero.Bootstrap
{
    public sealed class BootstrapPipeline
    {
        private readonly IReadOnlyList<IBootstrapStep> _steps;
        private readonly ILogService _log;
        private readonly IBootstrapProgressReporter _reporter;
        private readonly IEventBus _eventBus;
        private readonly IBootstrapReport _report;

        public BootstrapPipeline(
            IReadOnlyList<IBootstrapStep> steps,
            ILogService log,
            IBootstrapProgressReporter reporter,
            IEventBus eventBus = null,
            IBootstrapReport report = null)
        {
            _steps = steps;
            _log = log;
            _reporter = reporter;
            _eventBus = eventBus;
            _report = report;
        }

        public async UniTask RunAsync(IProgress<float> overallProgress, CancellationToken ct)
        {
            // A retry re-runs every step, so last run's failures must not stick.
            _report?.Clear();

            for (int i = 0; i < _steps.Count; i++)
            {
                var step = _steps[i];
                ct.ThrowIfCancellationRequested();

                _log.Info($"[Bootstrap] Step {i + 1}/{_steps.Count}: {step.Name}");
                _reporter?.Report((float)i / _steps.Count, step.Name);

                int stepIndex = i;
                var slice = new Progress<float>(p =>
                {
                    float overall = (stepIndex + Mathf.Clamp01(p)) / _steps.Count;
                    _reporter?.Report(overall, step.Name);
                    overallProgress?.Report(overall);
                });

                int attempts = Math.Max(1, step.MaxRetries + 1); // MaxRetries = additional tries beyond the first
                Exception lastError = null;
                bool succeeded = false;

                for (int attempt = 1; attempt <= attempts; attempt++)
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    try
                    {
                        if (step.Timeout > TimeSpan.Zero) cts.CancelAfter(step.Timeout);
                        await step.ExecuteAsync(slice, cts.Token);
                        succeeded = true;
                        lastError = null;
                        break;
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        // Outer cancellation always wins; never retry.
                        throw;
                    }
                    catch (OperationCanceledException) when (cts.IsCancellationRequested)
                    {
                        lastError = new TimeoutException($"Step '{step.Name}' timed out after {step.Timeout}.");
                        _log.Warn($"[Bootstrap] Step '{step.Name}' attempt {attempt}/{attempts} timed out.");
                        if (step.IsCritical) throw Abort(step.Name, attempt, lastError);
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        _log.Error(ex, $"[Bootstrap] Step '{step.Name}' attempt {attempt}/{attempts} failed");
                        if (step.IsCritical) throw Abort(step.Name, attempt, ex);
                    }
                }

                if (!succeeded && lastError != null)
                {
                    // Non-critical: continue so the player still reaches the game. Not
                    // silent, though — record it durably and announce it, so a feature
                    // that depends on this service can turn itself off instead of
                    // failing in the player's hands.
                    _log.Warn($"[Bootstrap] Step '{step.Name}' exhausted {attempts} attempt(s); continuing degraded.");
                    _report?.Record(step.Name, lastError, attempts);
                    _eventBus?.Publish(new BootstrapStepDegraded(step.Name, lastError, attempts));
                }

                float done = (i + 1f) / _steps.Count;
                _reporter?.Report(done, step.Name);
                overallProgress?.Report(done);
            }
        }

        // Critical abort: publish step identity for IEventBus subscribers (consumer
        // loading screens hook BootstrapFailed → retry UX) and surface it to catch
        // sites without string parsing.
        private BootstrapStepFailedException Abort(string stepName, int attempt, Exception error)
        {
            _eventBus?.Publish(new BootstrapFailed(stepName, error, attempt));
            return new BootstrapStepFailedException(stepName, attempt, error);
        }
    }
}
