using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zero.Core;
using Zero.Core.Events;

namespace Zero.Bootstrap
{
    /// <summary>
    /// Runs the bootstrap steps in two phases.
    ///
    /// <b>Blocking</b> steps run first, sequentially, under a whole-phase budget. The player
    /// is looking at a splash screen for exactly this long. When the phase ends —
    /// successfully, degraded, or out of budget — <c>BootstrapReady</c> publishes and
    /// <see cref="RunAsync"/> returns, so the game can start.
    ///
    /// <b>Deferred</b> steps run afterwards, still sequentially, while the player is already
    /// in the game. Sequential rather than concurrent on purpose: the tail costs the player
    /// nothing, and running in declared order preserves every ordering constraint between
    /// steps for free (RemoteConfig before VersionCheck, for one) without a dependency API
    /// and without interleaving services that were never written for it.
    ///
    /// Failure handling is identical in both phases — retry, then record in
    /// <see cref="IBootstrapReport"/> and publish <c>BootstrapStepDegraded</c>. No shipped
    /// step is critical; see docs/architecture/bootstrap-pipeline.md.
    /// </summary>
    public sealed class BootstrapPipeline
    {
        /// <summary>
        /// Ceiling on the whole blocking phase. This — not the per-step timeout — is what
        /// bounds time-to-first-screen, and it holds however many blocking steps a consumer
        /// adds. On expiry, unfinished and unstarted blocking steps are recorded degraded and
        /// the player is let through.
        /// </summary>
        public static readonly TimeSpan DefaultBlockingBudget = TimeSpan.FromSeconds(5);

        private readonly IReadOnlyList<IBootstrapStep> _steps;
        private readonly ILogService _log;
        private readonly IBootstrapProgressReporter _reporter;
        private readonly IEventBus _eventBus;
        private readonly IBootstrapReport _report;
        private readonly TimeSpan _blockingBudget;

        private UniTask _deferred = UniTask.CompletedTask;

        public BootstrapPipeline(
            IReadOnlyList<IBootstrapStep> steps,
            ILogService log,
            IBootstrapProgressReporter reporter,
            IEventBus eventBus = null,
            IBootstrapReport report = null,
            TimeSpan blockingBudget = default)
        {
            _steps = steps;
            _log = log;
            _reporter = reporter;
            _eventBus = eventBus;
            _report = report;
            _blockingBudget = blockingBudget > TimeSpan.Zero ? blockingBudget : DefaultBlockingBudget;
        }

        /// <summary>
        /// Completes when the deferred phase finishes. Nothing needs to await this to play the
        /// game — it exists for tests and for consumers that want to know everything is up.
        /// Gate features on <see cref="IBootstrapReport"/> instead of waiting on this.
        /// </summary>
        public UniTask DeferredCompletion => _deferred;

        /// <summary>
        /// Runs the blocking phase and returns. The deferred phase is left running in the
        /// background; observe it via <see cref="DeferredCompletion"/> if you need to.
        /// </summary>
        public async UniTask RunAsync(IProgress<float> overallProgress, CancellationToken ct)
        {
            // A retry re-runs every step, so last run's failures must not stick.
            _report?.Clear();

            var blocking = new List<IBootstrapStep>();
            var deferred = new List<IBootstrapStep>();
            for (int i = 0; i < _steps.Count; i++)
            {
                if (_steps[i].Phase == BootstrapPhase.Deferred) deferred.Add(_steps[i]);
                else blocking.Add(_steps[i]);
            }

            var watch = Stopwatch.StartNew();

            using (var budgetCts = new CancellationTokenSource(_blockingBudget))
            {
                await RunPhaseAsync(blocking, "blocking", overallProgress, ct, budgetCts.Token, reportProgress: true);
            }

            watch.Stop();
            _log.Info($"[Bootstrap] Ready in {watch.Elapsed.TotalMilliseconds:F0}ms " +
                      $"({blocking.Count} blocking step(s)); {deferred.Count} deferred step(s) continue in background.");
            _eventBus?.Publish(new BootstrapReady(watch.Elapsed.TotalMilliseconds));

            // Deliberately not awaited: the player goes now. Preserve() so the task can be
            // awaited more than once via DeferredCompletion.
            _deferred = RunDeferredAsync(deferred, ct).Preserve();
        }

        private async UniTask RunDeferredAsync(IReadOnlyList<IBootstrapStep> deferred, CancellationToken ct)
        {
            // Yield first, always. Awaiting a completed UniTask continues inline, so a phase
            // whose steps are all synchronous would otherwise run to completion inside this
            // call — before RunAsync returns — which is exactly the blocking behaviour the
            // phase split exists to prevent. One frame of latency here costs nothing; the
            // player is already in the game.
            await UniTask.Yield();
            await RunPhaseAsync(deferred, "deferred", null, ct, CancellationToken.None, reportProgress: false);
        }

        private async UniTask RunPhaseAsync(
            IReadOnlyList<IBootstrapStep> steps,
            string phaseName,
            IProgress<float> overallProgress,
            CancellationToken ct,
            CancellationToken budgetCt,
            bool reportProgress)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                ct.ThrowIfCancellationRequested();

                if (budgetCt.IsCancellationRequested)
                {
                    // Budget already gone — do not even start. Record so the game can tell the
                    // difference between "this service is fine" and "we never got to it".
                    _log.Warn($"[Bootstrap] Blocking budget exhausted; skipping '{step.Name}'.");
                    _report?.Record(step.Name, new TimeoutException(
                        $"Blocking budget of {_blockingBudget.TotalSeconds:F1}s expired before step '{step.Name}' started."), 0);
                    _eventBus?.Publish(new BootstrapStepDegraded(step.Name, new TimeoutException(
                        $"Blocking budget expired before step '{step.Name}' started."), 0));
                    continue;
                }

                _log.Info($"[Bootstrap] Step {i + 1}/{steps.Count} ({phaseName}): {step.Name}");
                ReportProgress(reportProgress, overallProgress, steps.Count, i, 0f, step.Name);

                int stepIndex = i;
                var slice = new Progress<float>(p =>
                    ReportProgress(reportProgress, overallProgress, steps.Count, stepIndex, Mathf.Clamp01(p), step.Name));

                int attempts = Math.Max(1, step.MaxRetries + 1); // MaxRetries = additional tries beyond the first
                Exception lastError = null;
                bool succeeded = false;
                int spent = 0;

                for (int attempt = 1; attempt <= attempts; attempt++)
                {
                    spent = attempt;
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct, budgetCt);
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
                    catch (OperationCanceledException) when (budgetCt.IsCancellationRequested)
                    {
                        lastError = new TimeoutException(
                            $"Blocking budget of {_blockingBudget.TotalSeconds:F1}s expired during step '{step.Name}'.");
                        _log.Warn($"[Bootstrap] Blocking budget expired during '{step.Name}'; letting the player through.");
                        break; // no retry — the budget is spent for every remaining step too
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
                    _log.Warn($"[Bootstrap] Step '{step.Name}' exhausted {spent} attempt(s); continuing degraded.");
                    _report?.Record(step.Name, lastError, spent);
                    _eventBus?.Publish(new BootstrapStepDegraded(step.Name, lastError, spent));
                }

                ReportProgress(reportProgress, overallProgress, steps.Count, i, 1f, step.Name);
            }

            // A phase with no steps still finishes at 1.0 — a loading bar must not sit at 0.
            if (steps.Count == 0) ReportProgress(reportProgress, overallProgress, 1, 0, 1f, phaseName);
        }

        // Progress describes the BLOCKING phase only: it is the bar the player is watching,
        // and it reaches 1.0 exactly when they are let through. The deferred phase passes a
        // null overallProgress and reports nothing.
        private void ReportProgress(bool enabled, IProgress<float> overallProgress, int count, int index, float withinStep, string stepName)
        {
            if (!enabled) return;
            if (overallProgress == null && _reporter == null) return;
            if (count <= 0) return;

            float value = (index + Mathf.Clamp01(withinStep)) / count;
            _reporter?.Report(value, stepName);
            overallProgress?.Report(value);
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
