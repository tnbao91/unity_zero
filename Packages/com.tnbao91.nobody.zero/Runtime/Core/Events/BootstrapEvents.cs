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

    // Published on IEventBus when a non-critical step exhausts its retries. The
    // pipeline continues and the player still reaches the game — this event exists
    // so "continue degraded" is an observable decision rather than a silent one.
    // Subscribe to disable the feature that depends on the failed service (hide the
    // shop if IAP degraded, skip the daily-reward popup if RemoteConfig degraded).
    // The durable equivalent is IBootstrapReport, which survives past the publish.
    public readonly struct BootstrapStepDegraded
    {
        public readonly string StepName;
        public readonly Exception Error;
        public readonly int Attempts;

        public BootstrapStepDegraded(string stepName, Exception error, int attempts)
        {
            StepName = stepName;
            Error = error;
            Attempts = attempts;
        }
    }

    // Publish from consumer code to request a full pipeline re-run after a
    // BootstrapFailed. The whole pipeline runs again, including steps that already
    // succeeded — steps must be idempotent (see docs/dev/PITFALLS.md).
    public readonly struct BootstrapRetryRequested
    {
    }
}
