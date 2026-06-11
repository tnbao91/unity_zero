using System;

namespace Zero.Core.Events
{
    // Published on IEventBus when the bootstrap pipeline aborts — a critical step
    // failed or timed out. Subscribe from a consumer loading screen to surface a
    // retry UI, then publish BootstrapRetryRequested to re-run the pipeline.
    public readonly struct BootstrapFailed
    {
        public readonly string StepName;
        public readonly Exception Error;
        public readonly int Attempt;

        public BootstrapFailed(string stepName, Exception error, int attempt)
        {
            StepName = stepName;
            Error = error;
            Attempt = attempt;
        }
    }

    // Publish from consumer code to request a full pipeline re-run after a
    // BootstrapFailed. The whole pipeline runs again, including steps that already
    // succeeded — steps must be idempotent (see docs/dev/PITFALLS.md).
    public readonly struct BootstrapRetryRequested
    {
    }
}
