using System;
using System.Collections.Generic;

namespace Zero.Core
{
    // One step that failed every attempt and was continued past.
    public readonly struct DegradedStep
    {
        public readonly string StepName;
        public readonly Exception Error;
        public readonly int Attempts;

        public DegradedStep(string stepName, Exception error, int attempts)
        {
            StepName = stepName;
            Error = error;
            Attempts = attempts;
        }
    }

    /// <summary>
    /// Durable record of which bootstrap steps failed. The template's steps are all
    /// non-critical by design — a failed init degrades a feature, it never denies the
    /// player the game — so "did anything break?" has to be answerable *after* boot,
    /// not only at the instant it broke.
    ///
    /// The BootstrapStepDegraded bus event fires once, when the failure happens.
    /// Anything constructed later (a shop screen, a daily-reward popup) has missed it,
    /// because R3EventBus does not replay. That code reads this instead.
    ///
    /// Written by BootstrapPipeline, read by everyone. Mirrors the read/write shape of
    /// IBootstrapProgressReporter.
    /// </summary>
    public interface IBootstrapReport
    {
        /// <summary>True when no step degraded during the last run.</summary>
        bool IsHealthy { get; }

        /// <summary>Steps that failed every attempt, in the order they failed.</summary>
        IReadOnlyList<DegradedStep> Degraded { get; }

        /// <summary>
        /// True if the named step degraded. Match is ordinal against IBootstrapStep.Name.
        /// Fails safe: a null or unknown name returns false.
        /// </summary>
        bool IsDegraded(string stepName);

        /// <summary>Pipeline hook — records a step that exhausted its retries.</summary>
        void Record(string stepName, Exception error, int attempts);

        /// <summary>
        /// Pipeline hook — drops all records. Called at the start of every run so a
        /// retry re-run does not inherit the previous run's failures.
        /// </summary>
        void Clear();
    }
}
