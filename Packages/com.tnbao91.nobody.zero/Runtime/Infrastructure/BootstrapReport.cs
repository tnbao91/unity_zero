using System;
using System.Collections.Generic;
using Zero.Core;

namespace Zero.Infrastructure
{
    /// <summary>
    /// Default <see cref="IBootstrapReport"/>. Written by BootstrapPipeline, read by
    /// anything that needs to know whether a service came up.
    ///
    /// Not thread-safe by design: the pipeline runs on the main thread and writes
    /// between awaits, and readers are Unity code on the same thread. A lock here
    /// would buy nothing.
    /// </summary>
    public sealed class BootstrapReport : IBootstrapReport
    {
        private readonly List<DegradedStep> _degraded = new List<DegradedStep>();

        public bool IsHealthy => _degraded.Count == 0;

        public IReadOnlyList<DegradedStep> Degraded => _degraded;

        public bool IsDegraded(string stepName)
        {
            if (string.IsNullOrEmpty(stepName)) return false;

            for (int i = 0; i < _degraded.Count; i++)
            {
                if (string.Equals(_degraded[i].StepName, stepName, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        public void Record(string stepName, Exception error, int attempts)
        {
            if (string.IsNullOrEmpty(stepName)) return;
            _degraded.Add(new DegradedStep(stepName, error, attempts));
        }

        public void Clear() => _degraded.Clear();
    }
}
